using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeaveManage.Services;

namespace LeaveManage.Tests;

/// <summary>Lịch nghỉ lễ: đếm ngày công trừ ngày lễ, và API quản lý (Admin thêm/xóa, người khác chỉ xem).</summary>
public class HolidayCalculationTests
{
    [Fact]
    public void Ngay_le_trong_tuan_khong_duoc_tinh()
    {
        // T2 7/12 đến T6 11/12/2026, trong đó T4 9/12 là ngày lễ → còn 4 ngày
        var holiday = new[] { new DateTime(2026, 12, 9) };
        Assert.Equal(4, LeaveDayCalculator.CountWorkingDays(new DateTime(2026, 12, 7), new DateTime(2026, 12, 11), holiday));
    }

    [Fact]
    public void Ngay_le_roi_vao_cuoi_tuan_khong_lam_thay_doi_ket_qua()
    {
        var weekendHoliday = new[] { new DateTime(2026, 12, 12) };
        Assert.Equal(5, LeaveDayCalculator.CountWorkingDays(new DateTime(2026, 12, 7), new DateTime(2026, 12, 11), weekendHoliday));
    }

    [Fact]
    public void Khong_truyen_ngay_le_giong_cach_cu()
    {
        Assert.Equal(5, LeaveDayCalculator.CountWorkingDays(new DateTime(2026, 12, 7), new DateTime(2026, 12, 11)));
    }
}

[Collection("api")]
public class HolidayApiTests
{
    private readonly ApiFixture _api;
    public HolidayApiTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Admin_them_va_xoa_ngay_le_nguoi_thuong_khong_duoc_them()
    {
        var admin = await _api.LoginAsync("admin");
        var staff = await _api.LoginAsync("nv003");
        // Năm 2099 để không đụng dữ liệu năm hiện tại của test khác
        var date = new DateTime(2099, 5, 4);

        var forbidden = await staff.PostAsJsonAsync("/api/holidays", new { date, name = "Thử" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var created = await admin.PostAsJsonAsync("/api/holidays", new { date, name = "  Lễ thử  " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var duplicate = await admin.PostAsJsonAsync("/api/holidays", new { date, name = "Trùng" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var list = await staff.GetFromJsonAsync<JsonElement>("/api/holidays?year=2099");
        Assert.Contains(list.EnumerateArray(), h => h.GetProperty("id").GetInt32() == id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/holidays/{id}")).StatusCode);
    }

    [Fact]
    public async Task Khong_khai_bao_ngay_le_vao_cuoi_tuan()
    {
        var admin = await _api.LoginAsync("admin");
        var response = await admin.PostAsJsonAsync("/api/holidays", new { date = new DateTime(2099, 5, 2), name = "Chủ nhật" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
