using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeaveManage.Services;

namespace LeaveManage.Tests;

public class LeaveDayCalculatorTests
{
    [Theory]
    [InlineData("2026-12-07", "2026-12-11", 5)]  // T2 → T6
    [InlineData("2026-12-11", "2026-12-14", 2)]  // T6 → T2 (bỏ T7, CN)
    [InlineData("2026-12-12", "2026-12-13", 0)]  // chỉ T7, CN
    [InlineData("2026-12-09", "2026-12-09", 1)]  // một ngày
    public void CountWorkingDays_SkipsWeekend(string start, string end, int expected) =>
        Assert.Equal(expected, LeaveDayCalculator.CountWorkingDays(DateTime.Parse(start), DateTime.Parse(end)));
}

[Collection("api")]
public class AuthTests
{
    private readonly ApiFixture _api;
    public AuthTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/api/users/login", new { username = "admin", password = "sai-mat-khau" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenRejected()
    {
        var client = _api.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/users/login", new { username = "nv004", password = ApiFixture.DemoPassword }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var oldRefresh = login.GetProperty("refreshToken").GetString();

        var first = await client.PostAsJsonAsync("/api/users/refresh", new { refreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var reuse = await client.PostAsJsonAsync("/api/users/refresh", new { refreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task NoToken_401_UserOnAdminApi_403_AnonymousRegister_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/leave-requests")).StatusCode);
        var user = await _api.LoginAsync("nv003");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/users")).StatusCode);
        var anon = await _api.CreateClient().PostAsJsonAsync("/api/users/register", new { username = "hacker", email = "h@x.vn", password = "123456", role = "Admin" });
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }

    [Fact]
    public async Task AccountWithoutEmployee_CannotCreateLeave_LockedAccountCannotLogin()
    {
        var admin = await _api.LoginAsync("admin");
        var created = await admin.PostAsJsonAsync("/api/users/register",
            new { username = "khongnv", email = "khongnv@example.test", password = ApiFixture.DemoPassword, role = "User" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var noEmployee = await _api.LoginAsync("khongnv");
        var leave = await noEmployee.PostAsJsonAsync("/api/leave-requests", new
        { leaveTypeId = 1, startDate = ApiFixture.Weekday(10), endDate = ApiFixture.Weekday(10), reason = "Thử" });
        Assert.Equal(HttpStatusCode.Forbidden, leave.StatusCode);

        var duplicateLink = await admin.PostAsJsonAsync("/api/users/register",
            new { username = "trunglink", email = "trung@example.test", password = ApiFixture.DemoPassword, employeeId = 3 });
        Assert.Equal(HttpStatusCode.Conflict, duplicateLink.StatusCode);

        var users = await admin.GetFromJsonAsync<JsonElement>("/api/users?searchTerm=admin");
        var adminId = users.GetProperty("items").EnumerateArray().First(u => u.GetProperty("username").GetString() == "admin").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{adminId}", new { isActive = false })).StatusCode);

        var lockResponse = await admin.PutAsJsonAsync($"/api/users/{userId}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        var login = await _api.CreateClient().PostAsJsonAsync("/api/users/login", new { username = "khongnv", password = ApiFixture.DemoPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}

[Collection("api")]
public class ScopeTests
{
    private readonly ApiFixture _api;
    public ScopeTests(ApiFixture api) => _api = api;

    private static async Task<List<int>> EmployeeIds(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/employees?pageSize=50");
        return body.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).OrderBy(x => x).ToList();
    }

    [Fact]
    public async Task EmployeeList_IsScopedByRole()
    {
        Assert.Equal(new List<int> { 3 }, await EmployeeIds(await _api.LoginAsync("nv003")));
        Assert.Equal(new List<int> { 2, 3, 4, 6 }, await EmployeeIds(await _api.LoginAsync("manager")));
        // Admin thấy toàn bộ; các test khác có thể tạo thêm nhân viên nên kiểm tra đủ 6 nhân viên mẫu
        var all = await EmployeeIds(await _api.LoginAsync("admin"));
        Assert.True(new[] { 1, 2, 3, 4, 5, 6 }.All(all.Contains));
    }

    [Fact]
    public async Task User_CannotReadOtherEmployee_Returns404()
    {
        var user = await _api.LoginAsync("nv003");
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync("/api/employees/4")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync("/api/employees/4/full")).StatusCode);
    }
}

[Collection("api")]
public class LeaveWorkflowTests
{
    private readonly ApiFixture _api;
    public LeaveWorkflowTests(ApiFixture api) => _api = api;

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static object Body(int leaveTypeId, DateTime start, DateTime end, string reason = "Việc gia đình") =>
        new { leaveTypeId, startDate = start, endDate = end, reason };

    private static async Task<int> Remaining(HttpClient client, int leaveTypeId, int year)
    {
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/leave-balances?year={year}&leaveTypeId={leaveTypeId}");
        return list.EnumerateArray().First().GetProperty("remainingDays").GetInt32();
    }

    private static async Task<int> CreateAndSubmit(HttpClient client, int leaveTypeId, DateTime start, DateTime end)
    {
        var created = await client.PostAsJsonAsync("/api/leave-requests", Body(leaveTypeId, start, end));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Json(created)).GetProperty("id").GetInt32();
        var submitted = await client.PostAsync($"/api/leave-requests/{id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        return id;
    }

    [Fact]
    public async Task FullFlow_Submit_Approve_Cancel_UpdatesBalanceCorrectly()
    {
        var employee = await _api.LoginAsync("nv003");
        var manager = await _api.LoginAsync("manager");
        var day = ApiFixture.Weekday(7);
        var before = await Remaining(employee, 1, day.Year);

        var id = await CreateAndSubmit(employee, 1, day, day);
        Assert.Equal(before - 1, await Remaining(employee, 1, day.Year));   // PendingDays giữ chỗ

        var approved = await manager.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { comment = "Đồng ý" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Approved", (await Json(approved)).GetProperty("status").GetString());
        Assert.Equal(before - 1, await Remaining(employee, 1, day.Year));   // chuyển sang UsedDays, không trừ thêm

        var detail = await employee.GetFromJsonAsync<JsonElement>($"/api/leave-requests/{id}");
        Assert.Equal(1, detail.GetProperty("approvals").GetArrayLength());

        var again = await manager.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);              // không xử lý 2 lần

        var cancelled = await employee.PostAsJsonAsync($"/api/leave-requests/{id}/cancel", new { reason = "Đổi lịch" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(before, await Remaining(employee, 1, day.Year));       // trả lại số dư
    }

    [Fact]
    public async Task Reject_ReleasesPendingDays_RequiresComment()
    {
        var employee = await _api.LoginAsync("nv004");
        var manager = await _api.LoginAsync("manager");
        var day = ApiFixture.Weekday(8);
        var before = await Remaining(employee, 1, day.Year);
        var id = await CreateAndSubmit(employee, 1, day, day);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.PostAsJsonAsync($"/api/leave-requests/{id}/reject", new { comment = "" })).StatusCode);
        var rejected = await manager.PostAsJsonAsync($"/api/leave-requests/{id}/reject", new { comment = "Thiếu người" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(before, await Remaining(employee, 1, day.Year));
    }

    [Fact]
    public async Task Validation_DateRange_Notice_WeekendOnly_CrossYear()
    {
        var employee = await _api.LoginAsync("nv003");
        var start = ApiFixture.Weekday(9);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await employee.PostAsJsonAsync("/api/leave-requests", Body(1, start, start.AddDays(-1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,   // phép năm phải báo trước 3 ngày
            (await employee.PostAsJsonAsync("/api/leave-requests", Body(1, DateTime.Today, DateTime.Today))).StatusCode);
        var saturday = DateTime.Today.AddDays(10);
        while (saturday.DayOfWeek != DayOfWeek.Saturday) saturday = saturday.AddDays(1);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await employee.PostAsJsonAsync("/api/leave-requests", Body(1, saturday, saturday.AddDays(1)))).StatusCode);
        var dec30 = new DateTime(DateTime.Today.Year, 12, 30);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await employee.PostAsJsonAsync("/api/leave-requests", Body(4, dec30, dec30.AddDays(5)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,   // nghỉ cưới tối đa 3 ngày
            (await employee.PostAsJsonAsync("/api/leave-requests", Body(5, start, ApiFixture.Weekday(16)))).StatusCode);
    }

    [Fact]
    public async Task Overlap_WithPendingRequest_Returns409()
    {
        var employee = await _api.LoginAsync("nv003");
        var day = ApiFixture.Weekday(14);
        await CreateAndSubmit(employee, 1, day, day);

        var second = await employee.PostAsJsonAsync("/api/leave-requests", Body(2, day, day));
        var id = (await Json(second)).GetProperty("id").GetInt32();
        var submit = await employee.PostAsync($"/api/leave-requests/{id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        Assert.Contains("OVERLAPPING_REQUEST", await submit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InsufficientBalance_Returns409()
    {
        var admin = await _api.LoginAsync("admin");
        var employee = await _api.LoginAsync("nv006");
        var day = ApiFixture.Weekday(15);
        var balances = await admin.GetFromJsonAsync<JsonElement>($"/api/leave-balances?employeeId=6&leaveTypeId=2&year={day.Year}");
        var balanceId = balances.EnumerateArray().First().GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/leave-balances/{balanceId}", new { initialDays = 0 })).StatusCode);

        var created = await employee.PostAsJsonAsync("/api/leave-requests", Body(2, day, day));
        var id = (await Json(created)).GetProperty("id").GetInt32();
        var submit = await employee.PostAsync($"/api/leave-requests/{id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        Assert.Contains("INSUFFICIENT_BALANCE", await submit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ownership_OtherUserCannotSubmitOrCancel_ManagerOnlyDirectReports()
    {
        var owner = await _api.LoginAsync("nv004");
        var other = await _api.LoginAsync("nv003");
        var manager = await _api.LoginAsync("manager");
        var day = ApiFixture.Weekday(17);
        var created = await owner.PostAsJsonAsync("/api/leave-requests", Body(1, day, day));
        var id = (await Json(created)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/leave-requests/{id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/leave-requests/{id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/leave-requests/{id}/submit", null)).StatusCode);

        // nv005 do Admin quản lý → Manager (NV002) không duyệt được
        var nv005 = await _api.LoginAsync("nv005");
        var id5 = await CreateAndSubmit(nv005, 1, day, day);
        var approve = await manager.PostAsJsonAsync($"/api/leave-requests/{id5}/approve", new { });
        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
    }

    [Fact]
    public async Task Concurrency_TwoApproversAtOnce_OnlyOneSucceeds_BalanceDeductedOnce()
    {
        var employee = await _api.LoginAsync("nv006");
        var manager = await _api.LoginAsync("manager");
        var admin = await _api.LoginAsync("admin");
        var day = ApiFixture.Weekday(20);
        var before = await Remaining(employee, 1, day.Year);
        var id = await CreateAndSubmit(employee, 1, day, day);

        var results = await Task.WhenAll(
            manager.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { comment = "M" }),
            admin.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { comment = "A" }));

        var codes = results.Select(r => r.StatusCode).OrderBy(c => (int)c).ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, codes);
        Assert.Equal(before - 1, await Remaining(employee, 1, day.Year));
        var detail = await employee.GetFromJsonAsync<JsonElement>($"/api/leave-requests/{id}");
        Assert.Equal(1, detail.GetProperty("approvals").GetArrayLength());
    }

    [Fact]
    public async Task Concurrency_TwoOverlappingSubmitsAtOnce_OnlyOneSucceeds()
    {
        var employee = await _api.LoginAsync("nv004");
        var day = ApiFixture.Weekday(22);
        var ids = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            var created = await employee.PostAsJsonAsync("/api/leave-requests", Body(1, day, day));
            ids.Add((await Json(created)).GetProperty("id").GetInt32());
        }

        var results = await Task.WhenAll(ids.Select(id => employee.PostAsync($"/api/leave-requests/{id}/submit", null)));
        var codes = results.Select(r => r.StatusCode).OrderBy(c => (int)c).ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, codes);
    }

    [Fact]
    public async Task Catalog_DuplicateCode409_UserCannotCreate403()
    {
        var admin = await _api.LoginAsync("admin");
        var user = await _api.LoginAsync("nv003");
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync("/api/departments", new { name = "Trùng", code = "it" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.PostAsJsonAsync("/api/leave-types", new { name = "X", code = "X1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await admin.PostAsJsonAsync("/api/positions", new { name = "Chuyên viên kiểm thử", code = "QA", level = 2 })).StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsScopedCounts()
    {
        var manager = await _api.LoginAsync("manager");
        var body = await manager.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(4, body.GetProperty("employeeCount").GetInt32());
        Assert.True(body.GetProperty("myBalances").GetArrayLength() >= 1);
    }
}
