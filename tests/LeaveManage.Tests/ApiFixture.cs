using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace LeaveManage.Tests;

/// <summary>
/// Chạy API thật (WebApplicationFactory) trên database SQL Server riêng LeaveManage_Test.
/// Mỗi lần chạy test: xóa và tạo lại đúng database có tên LeaveManage_Test (có kiểm tra tên),
/// không bao giờ đụng tới LeaveManageDb.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestDatabase = "LeaveManage_Test";
    public const string DemoPassword = "Test-Password-123";
    private const string SqlServerInstance = @"(localdb)\MSSQLLocalDB";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string ConnectionString(string database) =>
        $"Server={SqlServerInstance};Database={database};Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString(TestDatabase));
        builder.UseSetting("Jwt:Key", "test-only-key-0123456789-abcdefghijklmnopqrstuvwxyz");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
    }

    public async Task InitializeAsync()
    {
        if (TestDatabase != "LeaveManage_Test")
            throw new InvalidOperationException("Guard: chỉ được xóa database LeaveManage_Test.");

        await using var connection = new SqlConnection(ConnectionString("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{TestDatabase}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{TestDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{TestDatabase}];
            END
            """;
        await command.ExecuteNonQueryAsync();

        // Khởi động host → Program tự migrate + seed vào LeaveManage_Test
        CreateClient().Dispose();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public async Task<HttpClient> LoginAsync(string username)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/users/login", new { username, password = DemoPassword });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    /// <summary>Ngày làm việc (T2–T6) gần nhất kể từ hôm nay + offset ngày.</summary>
    public static DateTime Weekday(int offsetDays)
    {
        var day = DateTime.Today.AddDays(offsetDays);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        return day;
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
