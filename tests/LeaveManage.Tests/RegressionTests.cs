using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LeaveManage.Tests;

/// <summary>
/// Regression test cho các lỗi do audit độc lập (CODEX_FINAL_AUDIT.md) phát hiện:
/// P1-01 sửa nháp đua với gửi duyệt; P1-02 JWT cũ sau khóa/logout/đổi quyền;
/// P1-03 refresh token rotation không nguyên tử; P2-01 số trang cực lớn.
/// Mỗi test tự tạo nhân viên/tài khoản riêng để không ảnh hưởng dữ liệu của test khác.
/// </summary>
[Collection("api")]
public class RegressionTests
{
    private readonly ApiFixture _api;
    private static int _sequence;

    public RegressionTests(ApiFixture api) => _api = api;

    // ===================== Hỗ trợ =====================

    private static string Unique(string prefix) => $"{prefix}{Interlocked.Increment(ref _sequence)}{Random.Shared.Next(1000, 9999)}";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> CreateEmployeeAsync(HttpClient admin, int managerId)
    {
        var code = Unique("RG");
        var response = await admin.PostAsJsonAsync("/api/employees", new
        {
            employeeCode = code, fullName = $"Nhân viên {code}", email = $"{code.ToLowerInvariant()}@example.test",
            dateOfBirth = new DateTime(1995, 1, 1), hireDate = new DateTime(2024, 1, 1),
            departmentId = 1, positionId = 1, managerId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetInt32();
    }

    private async Task<(int UserId, string Username)> CreateAccountAsync(HttpClient admin, int employeeId, string role = "User")
    {
        var username = Unique("acc");
        var response = await admin.PostAsJsonAsync("/api/users/register", new
        {
            username, email = $"{username}@example.test", password = ApiFixture.DemoPassword, role, employeeId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await Json(response)).GetProperty("id").GetInt32(), username);
    }

    private static async Task GrantBalanceAsync(HttpClient admin, int employeeId, int leaveTypeId, int days = 100)
    {
        var response = await admin.PostAsJsonAsync("/api/leave-balances", new { employeeId, leaveTypeId, year = DateTime.Today.Year, initialDays = days });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(HttpStatusCode Status, string? Token, string? Refresh)> LoginRawAsync(string username)
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/api/users/login", new { username, password = ApiFixture.DemoPassword });
        if (!response.IsSuccessStatusCode) return (response.StatusCode, null, null);
        var body = await Json(response);
        return (response.StatusCode, body.GetProperty("token").GetString(), ApiFixture.RefreshCookie(response));
    }

    private HttpClient ClientWithToken(string? token)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpResponseMessage> RefreshAsync(string? refreshToken) =>
        await _api.CreateClient().SendAsync(ApiFixture.RefreshRequest(refreshToken));

    /// <summary>(Pending, Used) của nhân viên theo loại nghỉ trong năm hiện tại, đọc bằng tài khoản Admin.</summary>
    private static async Task<(int Pending, int Used)> BalanceAsync(HttpClient admin, int employeeId, int leaveTypeId)
    {
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/leave-balances?employeeId={employeeId}&leaveTypeId={leaveTypeId}&year={DateTime.Today.Year}");
        var b = list.EnumerateArray().Single();
        return (b.GetProperty("pendingDays").GetInt32(), b.GetProperty("usedDays").GetInt32());
    }

    /// <summary>Nhân viên mới + tài khoản User + số dư phép năm và phép ốm. Quản lý trực tiếp là NV001 (admin, cũng là người duyệt)
    /// để không làm thay đổi danh sách cấp dưới của "manager" mà các test phạm vi khác kiểm tra.</summary>
    private async Task<(HttpClient Admin, HttpClient Owner, HttpClient Approver, int EmployeeId)> SetupEmployeeAsync()
    {
        var admin = await _api.LoginAsync("admin");
        var employeeId = await CreateEmployeeAsync(admin, managerId: 1);
        var (_, username) = await CreateAccountAsync(admin, employeeId);
        await GrantBalanceAsync(admin, employeeId, 1);
        await GrantBalanceAsync(admin, employeeId, 2);
        return (admin, await _api.LoginAsync(username), admin, employeeId);
    }

    private static object Leave(int leaveTypeId, DateTime start, DateTime end) =>
        new { leaveTypeId, startDate = start, endDate = end, reason = "Kiểm thử hồi quy" };

    // ===================== P1-01 =====================

    [Fact]
    public async Task P1_01_EditDaysWhileSubmitting_BalanceAlwaysMatchesRequest()
    {
        var (admin, owner, manager, employeeId) = await SetupEmployeeAsync();
        var day1 = ApiFixture.Weekday(10);
        var day2 = day1.AddDays(1);
        while (day2.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day2 = day2.AddDays(1);

        for (var round = 0; round < 15; round++)
        {
            var created = await owner.PostAsJsonAsync("/api/leave-requests", Leave(1, day1, day1));
            var id = (await Json(created)).GetProperty("id").GetInt32();

            // Sửa 1 ngày → 2 ngày và gửi duyệt CÙNG LÚC
            var edit = owner.PutAsJsonAsync($"/api/leave-requests/{id}", Leave(1, day1, day2));
            var submit = owner.PostAsync($"/api/leave-requests/{id}/submit", null);
            await Task.WhenAll(edit, submit);

            Assert.Equal(HttpStatusCode.OK, (await submit).StatusCode);
            Assert.Contains((await edit).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });

            var request = await owner.GetFromJsonAsync<JsonElement>($"/api/leave-requests/{id}");
            var totalDays = request.GetProperty("totalDays").GetInt32();
            Assert.Equal("Pending", request.GetProperty("status").GetString());
            // Sửa thắng (200) → submit tính theo 2 ngày; submit thắng → sửa bị 409, giữ 1 ngày
            Assert.Equal((await edit).StatusCode == HttpStatusCode.OK ? 2 : 1, totalDays);
            Assert.Equal((totalDays, 0), await BalanceAsync(admin, employeeId, 1));

            // Đơn vẫn duyệt và hủy bình thường, số dư về đúng 0
            Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { comment = "OK" })).StatusCode);
            Assert.Equal((0, totalDays), await BalanceAsync(admin, employeeId, 1));
            Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/api/leave-requests/{id}/cancel", new { reason = "Hủy" })).StatusCode);
            Assert.Equal((0, 0), await BalanceAsync(admin, employeeId, 1));
        }
    }

    [Fact]
    public async Task P1_01_EditLeaveTypeWhileSubmitting_PendingOnCorrectBalance()
    {
        var (admin, owner, manager, employeeId) = await SetupEmployeeAsync();
        var day = ApiFixture.Weekday(12);

        for (var round = 0; round < 15; round++)
        {
            var created = await owner.PostAsJsonAsync("/api/leave-requests", Leave(1, day, day));
            var id = (await Json(created)).GetProperty("id").GetInt32();

            // Đổi Phép năm (1) → Phép ốm (2) và gửi duyệt CÙNG LÚC
            var edit = owner.PutAsJsonAsync($"/api/leave-requests/{id}", Leave(2, day, day));
            var submit = owner.PostAsync($"/api/leave-requests/{id}/submit", null);
            await Task.WhenAll(edit, submit);
            Assert.Equal(HttpStatusCode.OK, (await submit).StatusCode);

            var request = await owner.GetFromJsonAsync<JsonElement>($"/api/leave-requests/{id}");
            var typeId = request.GetProperty("leaveTypeId").GetInt32();
            Assert.Equal((await edit).StatusCode == HttpStatusCode.OK ? 2 : 1, typeId);
            var other = typeId == 1 ? 2 : 1;
            Assert.Equal((1, 0), await BalanceAsync(admin, employeeId, typeId)); // giữ chỗ đúng loại nghỉ của đơn
            Assert.Equal((0, 0), await BalanceAsync(admin, employeeId, other));

            if (round % 2 == 0)
            {
                Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { })).StatusCode);
                Assert.Equal((0, 1), await BalanceAsync(admin, employeeId, typeId));
            }
            Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/api/leave-requests/{id}/cancel", new { })).StatusCode);
            Assert.Equal((0, 0), await BalanceAsync(admin, employeeId, typeId));
        }
    }

    // ===================== P1-02 =====================

    [Fact]
    public async Task P1_02_LockedManager_OldJwtCannotReadOrApprove_AndDoesNotReviveAfterUnlock()
    {
        var admin = await _api.LoginAsync("admin");
        var managerEmployee = await CreateEmployeeAsync(admin, managerId: 1);
        var (managerUserId, managerUsername) = await CreateAccountAsync(admin, managerEmployee, "Manager");
        var staffEmployee = await CreateEmployeeAsync(admin, managerId: managerEmployee);
        var (_, staffUsername) = await CreateAccountAsync(admin, staffEmployee);
        await GrantBalanceAsync(admin, staffEmployee, 1);

        var staff = await _api.LoginAsync(staffUsername);
        var created = await staff.PostAsJsonAsync("/api/leave-requests", Leave(1, ApiFixture.Weekday(10), ApiFixture.Weekday(10)));
        var id = (await Json(created)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/api/leave-requests/{id}/submit", null)).StatusCode);

        var (_, oldToken, oldRefresh) = await LoginRawAsync(managerUsername);
        var oldManager = ClientWithToken(oldToken);
        Assert.Equal(HttpStatusCode.OK, (await oldManager.GetAsync("/api/leave-requests/pending-approval")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{managerUserId}", new { isActive = false })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await oldManager.GetAsync("/api/leave-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldManager.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { })).StatusCode);
        Assert.Equal("Pending", (await staff.GetFromJsonAsync<JsonElement>($"/api/leave-requests/{id}")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginRawAsync(managerUsername)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(oldRefresh)).StatusCode);

        // Mở khóa: token cũ KHÔNG sống lại; đăng nhập mới thì làm việc bình thường
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{managerUserId}", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldManager.GetAsync("/api/leave-requests")).StatusCode);
        var newManager = await _api.LoginAsync(managerUsername);
        Assert.Equal(HttpStatusCode.OK, (await newManager.PostAsJsonAsync($"/api/leave-requests/{id}/approve", new { })).StatusCode);
    }

    [Fact]
    public async Task P1_02_Logout_RevokesAccessToken_EvenAfterNewLogin()
    {
        var admin = await _api.LoginAsync("admin");
        var employeeId = await CreateEmployeeAsync(admin, managerId: 1);
        var (_, username) = await CreateAccountAsync(admin, employeeId);

        var (_, token1, refresh1) = await LoginRawAsync(username);
        var session1 = ClientWithToken(token1);
        Assert.Equal(HttpStatusCode.OK, (await session1.GetAsync("/api/leave-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await session1.PostAsync("/api/users/logout", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session1.GetAsync("/api/leave-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(refresh1)).StatusCode);

        var (status, token2, _) = await LoginRawAsync(username);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(HttpStatusCode.OK, (await ClientWithToken(token2).GetAsync("/api/leave-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session1.GetAsync("/api/leave-requests")).StatusCode);
    }

    [Fact]
    public async Task P1_02_RoleOrEmployeeLinkChange_InvalidatesOldJwt()
    {
        var admin = await _api.LoginAsync("admin");
        var employeeA = await CreateEmployeeAsync(admin, managerId: 1);
        var employeeB = await CreateEmployeeAsync(admin, managerId: 1);
        var (userId, username) = await CreateAccountAsync(admin, employeeA);

        var (_, userToken, _) = await LoginRawAsync(username);
        var asUser = ClientWithToken(userToken);
        Assert.Equal(HttpStatusCode.OK, (await asUser.GetAsync("/api/users/me")).StatusCode);

        // Đổi role User → Manager: token mang role cũ bị từ chối
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{userId}", new { role = "Manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await asUser.GetAsync("/api/users/me")).StatusCode);
        var (_, managerToken, _) = await LoginRawAsync(username);
        var asManager = ClientWithToken(managerToken);
        Assert.Equal("Manager", (await asManager.GetFromJsonAsync<JsonElement>("/api/users/me")).GetProperty("role").GetString());

        // Đổi nhân viên liên kết A → B: token mang EmployeeId cũ bị từ chối
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{userId}", new { employeeId = employeeB })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await asManager.GetAsync("/api/users/me")).StatusCode);
        var (_, linkedToken, _) = await LoginRawAsync(username);
        Assert.Equal(employeeB, (await ClientWithToken(linkedToken).GetFromJsonAsync<JsonElement>("/api/users/me")).GetProperty("employeeId").GetInt32());
    }

    // ===================== P1-03 =====================

    [Fact]
    public async Task P1_03_ConcurrentRefreshWithSameToken_ExactlyOneWins()
    {
        var admin = await _api.LoginAsync("admin");
        var employeeId = await CreateEmployeeAsync(admin, managerId: 1);
        var (_, username) = await CreateAccountAsync(admin, employeeId);

        for (var round = 0; round < 5; round++)
        {
            var (_, _, refresh) = await LoginRawAsync(username);
            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RefreshAsync(refresh)));

            var winners = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
            Assert.Single(winners);
            Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));

            var winner = await Json(winners[0]);
            Assert.Equal(HttpStatusCode.OK, (await ClientWithToken(winner.GetProperty("token").GetString()).GetAsync("/api/users/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(refresh)).StatusCode);               // token cũ không dùng lại
            Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(ApiFixture.RefreshCookie(winners[0]))).StatusCode); // token mới dùng được
        }
    }

    [Fact]
    public async Task P1_03_RefreshRacingLogout_NoUsableTokenSurvives()
    {
        var admin = await _api.LoginAsync("admin");
        var employeeId = await CreateEmployeeAsync(admin, managerId: 1);
        var (_, username) = await CreateAccountAsync(admin, employeeId);

        for (var round = 0; round < 8; round++)
        {
            var (_, token, refresh) = await LoginRawAsync(username);
            var refreshTask = RefreshAsync(refresh);
            var logoutTask = ClientWithToken(token).PostAsync("/api/users/logout", null);
            await Task.WhenAll(refreshTask, logoutTask);

            Assert.Equal(HttpStatusCode.NoContent, (await logoutTask).StatusCode);
            await AssertNoSurvivingSession((await refreshTask), refresh);
        }
    }

    [Fact]
    public async Task P1_03_RefreshRacingAccountLock_NoUsableTokenSurvives()
    {
        var admin = await _api.LoginAsync("admin");

        for (var round = 0; round < 5; round++)
        {
            var employeeId = await CreateEmployeeAsync(admin, managerId: 1);
            var (userId, username) = await CreateAccountAsync(admin, employeeId);
            var (_, _, refresh) = await LoginRawAsync(username);

            var refreshTask = RefreshAsync(refresh);
            var lockTask = admin.PutAsJsonAsync($"/api/users/{userId}", new { isActive = false });
            await Task.WhenAll(refreshTask, lockTask);

            Assert.Equal(HttpStatusCode.OK, (await lockTask).StatusCode);
            await AssertNoSurvivingSession((await refreshTask), refresh);
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginRawAsync(username)).Status);
        }
    }

    /// <summary>Sau khi thu hồi (logout/khóa) hoàn tất: mọi token đã phát (nếu refresh thắng trước) đều không dùng được.</summary>
    private async Task AssertNoSurvivingSession(HttpResponseMessage refreshResponse, string? oldRefresh)
    {
        Assert.Contains(refreshResponse.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        if (refreshResponse.StatusCode == HttpStatusCode.OK)
        {
            var issued = await Json(refreshResponse);
            Assert.Equal(HttpStatusCode.Unauthorized, (await ClientWithToken(issued.GetProperty("token").GetString()).GetAsync("/api/users/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(ApiFixture.RefreshCookie(refreshResponse))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(oldRefresh)).StatusCode);
    }

    // ===================== P2-01 =====================

    [Theory]
    [InlineData("/api/leave-requests?page=2147483647&pageSize=50")]
    [InlineData("/api/leave-requests?page=100001&pageSize=50")]
    [InlineData("/api/employees?page=2147483647&pageSize=100")]
    [InlineData("/api/users?pageNumber=2147483647&pageSize=50")]
    [InlineData("/api/leave-approvals?page=2147483647&pageSize=50")]
    [InlineData("/api/leave-requests/pending-approval?page=2147483647&pageSize=50")]
    public async Task P2_01_HugePageNumber_Returns400(string url)
    {
        var admin = await _api.LoginAsync("admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task P2_01_LastAllowedPage_ReturnsEmptyPage()
    {
        var admin = await _api.LoginAsync("admin");
        var body = await admin.GetFromJsonAsync<JsonElement>("/api/leave-requests?page=100000&pageSize=50");
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }
}
