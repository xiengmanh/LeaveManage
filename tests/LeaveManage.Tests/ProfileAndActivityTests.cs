using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LeaveManage.Tests;

/// <summary>Tự đổi mật khẩu/số điện thoại (mọi vai trò) và nhật ký hoạt động.</summary>
[Collection("api")]
public class ProfileAndActivityTests
{
    private readonly ApiFixture _api;
    private static int _seq;

    public ProfileAndActivityTests(ApiFixture api) => _api = api;

    private static string Unique(string p) => $"{p}{Interlocked.Increment(ref _seq)}{Random.Shared.Next(1000, 9999)}";

    private static Task<JsonElement> Body(HttpResponseMessage r) => r.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Tạo nhân viên + tài khoản riêng (không đụng tài khoản demo) rồi đăng nhập.</summary>
    private async Task<(HttpClient Client, string Username, string Password)> NewUserAsync(string role = "User")
    {
        var admin = await _api.LoginAsync("admin");
        var code = Unique("PF");
        var emp = await admin.PostAsJsonAsync("/api/employees", new
        {
            employeeCode = code, fullName = $"Nhân viên {code}", email = $"{code.ToLowerInvariant()}@example.test",
            dateOfBirth = new DateTime(1995, 1, 1), hireDate = new DateTime(2024, 1, 1), departmentId = 1, positionId = 1, managerId = (int?)null
        });
        Assert.Equal(HttpStatusCode.Created, emp.StatusCode);
        var employeeId = (await Body(emp)).GetProperty("id").GetInt32();

        var username = Unique("pfu");
        var reg = await admin.PostAsJsonAsync("/api/users/register", new
        {
            username, email = $"{username}@example.test", password = "MatKhau-Cu-123", role, employeeId
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        return (await LoginAsync(username, "MatKhau-Cu-123"), username, "MatKhau-Cu-123");
    }

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _api.CreateClient();
        var response = await client.PostAsJsonAsync("/api/users/login", new { username, password });
        response.EnsureSuccessStatusCode();
        var body = await Body(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    // ===================== Đổi mật khẩu =====================

    [Theory]
    [InlineData("User")]
    [InlineData("Manager")]
    public async Task Moi_vai_tro_doi_duoc_mat_khau_va_phien_cu_bi_thu_hoi(string role)
    {
        var (client, username, oldPassword) = await NewUserAsync(role);

        var wrong = await client.PutAsJsonAsync("/api/users/me/password", new { currentPassword = "sai-roi", newPassword = "MatKhau-Moi-456", confirmPassword = "MatKhau-Moi-456" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("WRONG_PASSWORD", (await Body(wrong)).GetProperty("errorCode").GetString());

        var ok = await client.PutAsJsonAsync("/api/users/me/password", new { currentPassword = oldPassword, newPassword = "MatKhau-Moi-456", confirmPassword = "MatKhau-Moi-456" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // JWT cũ hết hiệu lực ngay (TokenVersion), mật khẩu cũ không đăng nhập được, mật khẩu mới thì được
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
        var oldLogin = await _api.CreateClient().PostAsJsonAsync("/api/users/login", new { username, password = oldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var fresh = await LoginAsync(username, "MatKhau-Moi-456");
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Mat_khau_moi_khong_hop_le_bi_tu_choi()
    {
        var (client, _, oldPassword) = await NewUserAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/users/me/password",
            new { currentPassword = oldPassword, newPassword = "ngan", confirmPassword = "ngan" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/users/me/password",
            new { currentPassword = oldPassword, newPassword = "MatKhau-Moi-456", confirmPassword = "khac-nhau-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/users/me/password",
            new { currentPassword = oldPassword, newPassword = oldPassword, confirmPassword = oldPassword })).StatusCode);
        // Chưa đăng nhập thì không đổi được
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().PutAsJsonAsync("/api/users/me/password",
            new { currentPassword = "a", newPassword = "MatKhau-Moi-456", confirmPassword = "MatKhau-Moi-456" })).StatusCode);
    }

    // ===================== Đổi số điện thoại =====================

    [Fact]
    public async Task Doi_so_dien_thoai_hop_le_va_khong_hop_le()
    {
        var (client, _, _) = await NewUserAsync();

        var ok = await client.PutAsJsonAsync("/api/users/me/phone", new { phoneNumber = "090 123 4567" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var profile = await Body(await client.GetAsync("/api/users/me/profile"));
        Assert.Equal("0901234567", profile.GetProperty("phoneNumber").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/users/me/phone", new { phoneNumber = "abc123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/users/me/phone", new { phoneNumber = "12345" })).StatusCode);

        // Để trống = xóa số
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/users/me/phone", new { phoneNumber = "" })).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Body(await client.GetAsync("/api/users/me/profile"))).GetProperty("phoneNumber").ValueKind);
    }

    [Fact]
    public async Task Chi_doi_duoc_so_dien_thoai_cua_chinh_minh()
    {
        var (a, _, _) = await NewUserAsync();
        var (b, _, _) = await NewUserAsync();
        await a.PutAsJsonAsync("/api/users/me/phone", new { phoneNumber = "0911111111" });
        await b.PutAsJsonAsync("/api/users/me/phone", new { phoneNumber = "0922222222" });
        Assert.Equal("0911111111", (await Body(await a.GetAsync("/api/users/me/profile"))).GetProperty("phoneNumber").GetString());
    }

    // ===================== Nhật ký hoạt động =====================

    private static async Task<List<JsonElement>> LogsAsync(HttpClient client, string? search = null)
    {
        var r = await client.GetAsync($"/api/activity-logs?page=1&pageSize=50{(search is null ? "" : "&search=" + search)}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await Body(r)).GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task Nhat_ky_ghi_dang_nhap_va_thao_tac_va_moi_tai_khoan_chi_thay_cua_minh()
    {
        var (a, userA, _) = await NewUserAsync();
        var (b, userB, _) = await NewUserAsync();
        await a.PutAsJsonAsync("/api/users/me/phone", new { phoneNumber = "0933333333" });

        var logsA = await LogsAsync(a);
        Assert.All(logsA, l => Assert.Equal(userA, l.GetProperty("username").GetString()));
        Assert.Contains(logsA, l => l.GetProperty("action").GetString() == "Đăng nhập");
        Assert.Contains(logsA, l => l.GetProperty("action").GetString() == "Đổi số điện thoại" && l.GetProperty("isSuccess").GetBoolean());

        var logsB = await LogsAsync(b);
        Assert.DoesNotContain(logsB, l => l.GetProperty("username").GetString() == userA);

        // Admin thấy của tất cả
        var admin = await _api.LoginAsync("admin");
        Assert.NotEmpty(await LogsAsync(admin, userA));
        Assert.NotEmpty(await LogsAsync(admin, userB));
    }

    [Fact]
    public async Task Nhat_ky_ghi_dang_nhap_that_bai_va_khong_luu_mat_khau()
    {
        var (_, username, _) = await NewUserAsync();
        var bad = await _api.CreateClient().PostAsJsonAsync("/api/users/login", new { username, password = "SaiMatKhau-999" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var admin = await _api.LoginAsync("admin");
        var logs = await LogsAsync(admin, username);
        var failed = Assert.Single(logs, l => l.GetProperty("action").GetString() == "Đăng nhập thất bại");
        Assert.False(failed.GetProperty("isSuccess").GetBoolean());
        Assert.DoesNotContain("SaiMatKhau-999", failed.ToString());
    }

    [Fact]
    public async Task Nhat_ky_ghi_thao_tac_nghiep_vu_cua_admin_va_can_dang_nhap()
    {
        var admin = await _api.LoginAsync("admin");
        var code = Unique("LG");
        var created = await admin.PostAsJsonAsync("/api/employees", new
        {
            employeeCode = code, fullName = "Nhật Ký", email = $"{code.ToLowerInvariant()}@example.test",
            dateOfBirth = new DateTime(1995, 1, 1), hireDate = new DateTime(2024, 1, 1), departmentId = 1, positionId = 1, managerId = (int?)null
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains(await LogsAsync(admin, "admin"), l => l.GetProperty("action").GetString() == "Thêm nhân viên");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/activity-logs")).StatusCode);
    }
}
