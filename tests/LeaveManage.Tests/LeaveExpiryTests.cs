using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LeaveManage.Data;
using LeaveManage.Models;
using LeaveManage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveManage.Tests;

/// <summary>
/// Đơn Pending quá ngày bắt đầu chuyển sang Expired và trả lại số ngày đang giữ chỗ.
/// Test chỉ chạm tới đơn của chính nó (ngày bắt đầu đưa về quá khứ bằng EF), nên không ảnh hưởng test khác.
/// </summary>
[Collection("api")]
public class LeaveExpiryTests
{
    private readonly ApiFixture _api;
    private static int _sequence;

    public LeaveExpiryTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Don_pending_qua_ngay_bat_dau_chuyen_sang_expired_va_tra_lai_so_du()
    {
        var admin = await _api.LoginAsync("admin");
        var code = $"EXP{Interlocked.Increment(ref _sequence)}{Random.Shared.Next(1000, 9999)}";
        var emp = await admin.PostAsJsonAsync("/api/employees", new
        {
            employeeCode = code, fullName = $"Nhân viên {code}", email = $"{code.ToLowerInvariant()}@example.test",
            dateOfBirth = new DateTime(1995, 1, 1), hireDate = new DateTime(2024, 1, 1), departmentId = 1, positionId = 1, managerId = 1
        });
        Assert.Equal(HttpStatusCode.Created, emp.StatusCode);
        var employeeId = (await emp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var username = $"exp{code.ToLowerInvariant()}";
        var reg = await admin.PostAsJsonAsync("/api/users/register", new
        {
            username, email = $"{username}@example.test", password = ApiFixture.DemoPassword, role = "User", employeeId
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var grant = await admin.PostAsJsonAsync("/api/leave-balances", new { employeeId, leaveTypeId = 1, year = DateTime.Today.Year, initialDays = 10 });
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);

        var staff = await _api.LoginAsync(username);
        var day = ApiFixture.Weekday(10);
        var created = await staff.PostAsJsonAsync("/api/leave-requests", new { leaveTypeId = 1, startDate = day, endDate = day, reason = "Kiểm thử hết hạn" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/api/leave-requests/{id}/submit", null)).StatusCode);

        Assert.Equal(1, await BalancePendingAsync(admin, employeeId));

        // Đưa ngày bắt đầu về quá khứ (API không cho tạo đơn quá khứ, nên sửa trực tiếp trong DB của test)
        await MoveStartDateToPastAsync(id, DateTime.Today.AddDays(-1));

        int expired;
        using (var scope = _api.Services.CreateScope())
        {
            expired = await scope.ServiceProvider.GetRequiredService<ILeaveExpiryService>().ExpireOverdueAsync(DateTime.Today);
        }
        Assert.True(expired >= 1);

        var after = await staff.GetFromJsonAsync<JsonElement>($"/api/leave-requests/{id}");
        Assert.Equal("Expired", after.GetProperty("status").GetString());
        Assert.Equal(0, await BalancePendingAsync(admin, employeeId));

        // Chạy lại không đếm và không trừ/cộng số dư thêm lần nữa
        using (var scope = _api.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILeaveExpiryService>().ExpireOverdueAsync(DateTime.Today);
        }
        Assert.Equal(0, await BalancePendingAsync(admin, employeeId));
        Assert.Equal("Expired", (await staff.GetFromJsonAsync<JsonElement>($"/api/leave-requests/{id}")).GetProperty("status").GetString());
    }

    private async Task<int> BalancePendingAsync(HttpClient admin, int employeeId)
    {
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/leave-balances?employeeId={employeeId}&leaveTypeId=1&year={DateTime.Today.Year}");
        return list.EnumerateArray().Single().GetProperty("pendingDays").GetInt32();
    }

    private async Task MoveStartDateToPastAsync(int requestId, DateTime start)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.LeaveRequests.SingleAsync(r => r.Id == requestId);
        request.StartDate = start;
        request.EndDate = start;
        await db.SaveChangesAsync();
    }
}
