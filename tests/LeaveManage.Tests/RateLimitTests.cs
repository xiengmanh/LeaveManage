using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LeaveManage.Tests;

/// <summary>
/// Host riêng với giới hạn đăng nhập/làm mới thấp (3 yêu cầu/phút/IP) để kiểm tra 429.
/// Dùng cùng database LeaveManage_Test mà ApiFixture đã tạo; không xóa lại database.
/// </summary>
public sealed class LowLimitFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Server=(localdb)\\MSSQLLocalDB;Database={ApiFixture.TestDatabase};Trusted_Connection=True;TrustServerCertificate=True");
        builder.UseSetting("Jwt:Key", "test-only-key-0123456789-abcdefghijklmnopqrstuvwxyz");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DemoPassword", ApiFixture.DemoPassword);
        builder.UseSetting("RateLimit:AuthPerMinute", "3");
    }
}

[Collection("api")]
public class RateLimitTests
{
    // Mỗi test có host riêng để bộ đếm giới hạn không dùng chung. ApiFixture chỉ để đảm bảo database đã được tạo.
    public RateLimitTests(ApiFixture _) { }

    [Fact]
    public async Task Login_OverLimit_Returns429WithRetryAfterAndErrorCode()
    {
        using var factory = new LowLimitFactory();
        var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/users/login", new { username = "nv003", password = ApiFixture.DemoPassword });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, ok.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/users/login", new { username = "nv003", password = ApiFixture.DemoPassword });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("60", blocked.Headers.GetValues("Retry-After").Single());
        var body = await blocked.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        Assert.Equal("RATE_LIMIT_EXCEEDED", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Refresh_OverLimit_Returns429()
    {
        using var factory = new LowLimitFactory();
        var client = factory.CreateClient();
        // Các lượt trước trong cùng phút có thể đã dùng hết hạn mức của test khác cùng IP (localhost);
        // chỉ cần xác nhận lượt cuối cùng bị chặn.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
            last = await client.PostAsJsonAsync("/api/users/refresh", new { });

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
