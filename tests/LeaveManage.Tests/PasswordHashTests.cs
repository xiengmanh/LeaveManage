using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using LeaveManage.Data;
using LeaveManage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveManage.Tests;

/// <summary>Kiểm tra cơ chế băm mật khẩu mới (PBKDF2) và tương thích ngược với bản HMACSHA512 cũ.</summary>
public class PasswordHashTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void NewHash_RoundTrips_AndWrongPasswordFails()
    {
        _service.CreateHash("Mat-khau-moi-1", out var hash, out var salt);

        Assert.False(_service.IsLegacyHash(salt));
        Assert.True(_service.Verify("Mat-khau-moi-1", hash, salt));
        Assert.False(_service.Verify("Sai-mat-khau", hash, salt));
    }

    [Fact]
    public void NewHash_UsesRandomSalt_SoSamePasswordGivesDifferentHashes()
    {
        _service.CreateHash("Cung-mat-khau", out var hash1, out _);
        _service.CreateHash("Cung-mat-khau", out var hash2, out _);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void LegacyHmacSha512Hash_StillVerifies()
    {
        var (hash, salt) = LegacyHash("Mat-khau-cu-1");

        Assert.True(_service.IsLegacyHash(salt));
        Assert.True(_service.Verify("Mat-khau-cu-1", hash, salt));
        Assert.False(_service.Verify("Mat-khau-cu-2", hash, salt));
    }

    internal static (byte[] Hash, byte[] Salt) LegacyHash(string password)
    {
        using var hmac = new HMACSHA512();
        return (hmac.ComputeHash(Encoding.UTF8.GetBytes(password)), hmac.Key);
    }
}

[Collection("api")]
public class PasswordUpgradeTests
{
    private readonly ApiFixture _api;
    public PasswordUpgradeTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task LegacyHash_UpgradedToNewFormat_OnSuccessfulLogin()
    {
        var admin = await _api.LoginAsync("admin");
        var username = $"legacy{Random.Shared.Next(100000, 999999)}";
        var created = await admin.PostAsJsonAsync("/api/users/register", new
        {
            username, email = $"{username}@example.test", password = ApiFixture.DemoPassword, role = "User"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Giả lập tài khoản tạo từ trước khi đổi thuật toán: ghi bản băm HMACSHA512 trực tiếp vào DB
        var (legacyHash, legacySalt) = PasswordHashTests.LegacyHash(ApiFixture.DemoPassword);
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(u => u.Username == username)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, legacyHash)
                                          .SetProperty(u => u.PasswordSalt, legacySalt));
        }

        // Đăng nhập đúng → vẫn thành công và bản băm được nâng cấp
        var login = await _api.CreateClient().PostAsJsonAsync("/api/users/login", new { username, password = ApiFixture.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await verifyDb.Users.AsNoTracking().FirstAsync(u => u.Username == username);
        Assert.Empty(user.PasswordSalt);
        Assert.NotEqual(legacyHash, user.PasswordHash);
        Assert.Equal(HttpStatusCode.OK, (await _api.CreateClient().PostAsJsonAsync("/api/users/login", new { username, password = ApiFixture.DemoPassword })).StatusCode);
    }
}
