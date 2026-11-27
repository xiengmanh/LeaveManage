using LeaveManage.Authorization;
using LeaveManage.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Reflection;
using System.Text;
using LeaveManage.Data;
using LeaveManage.Middleware;
using LeaveManage.Options;
using LeaveManage.Repositories;
using LeaveManage.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// ===================== SERVICES =====================

// Add services to the container.
builder.Services.AddControllers(options => options.Filters.Add<LeaveManage.Filters.ActivityLogFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Configure SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories and services for one HTTP request.
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IFileService, FileService>();
// Nghiệp vụ nghỉ phép
builder.Services.AddScoped<ILeaveRepository, LeaveRepository>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<ILeaveAttachmentRepository, LeaveAttachmentRepository>();
builder.Services.AddScoped<ILeaveAttachmentService, LeaveAttachmentService>();
builder.Services.AddScoped<ILeaveBalanceService, LeaveBalanceService>();
builder.Services.AddScoped<ILeaveExpiryService, LeaveExpiryService>();
builder.Services.AddScoped<IHolidayRepository, HolidayRepository>();
builder.Services.AddScoped<IHolidayService, HolidayService>();
builder.Services.AddHostedService<LeaveExpiryBackgroundService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
// Danh mục: một repository generic dùng cho Department, Position, LeaveType
builder.Services.AddScoped(typeof(ICatalogRepository<>), typeof(CatalogRepository<>));
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddSingleton<IAuthorizationHandler, MinimumAgeHandler>();
builder.Services.Configure<FileUploadOptions>(
    builder.Configuration.GetSection(FileUploadOptions.SectionName));

// JWT: Issuer/Audience trong appsettings.json; Key bí mật lấy từ user-secrets (Development)
// hoặc biến môi trường Jwt__Key (Production). Không lưu Key trong repository.
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException(
        "Thiếu Jwt:Key (tối thiểu 32 byte). Chạy: dotnet user-secrets set \"Jwt:Key\" \"<chuỗi ngẫu nhiên>\"");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };
        // Chữ ký đúng chưa đủ: tài khoản phải còn tồn tại, còn hoạt động, đúng TokenVersion,
        // Role và EmployeeId. Nhờ vậy khóa tài khoản / logout / đổi quyền có hiệu lực ngay với JWT cũ.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                if (!await authService.IsAccessTokenCurrentAsync(context.Principal!))
                    context.Fail("Phiên đăng nhập đã bị thu hồi hoặc tài khoản đã thay đổi.");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(UserRoles.Admin));
    options.AddPolicy(
        AuthorizationPolicies.CanManageEmployees,
        policy => policy.RequireRole(UserRoles.Admin, UserRoles.Manager));
    options.AddPolicy(
        AuthorizationPolicies.CanApproveLeave,
        policy => policy.RequireRole(UserRoles.Admin, UserRoles.Manager));
    // Claims-based: chỉ tài khoản đã gắn nhân viên (có claim EmployeeId) mới tạo/gửi đơn được
    options.AddPolicy(
        AuthorizationPolicies.HasEmployee,
        policy => policy.RequireClaim(CustomClaimTypes.EmployeeId));
    options.AddPolicy(
        AuthorizationPolicies.MustBeHR,
        policy => policy.RequireClaim(CustomClaimTypes.Department, "HR"));
    options.AddPolicy(
        AuthorizationPolicies.MinimumAge18,
        policy => policy.AddRequirements(new MinimumAgeRequirement(18)));
});

// CORS: chỉ cho phép các origin cấu hình trong Cors:AllowedOrigins (không AllowAnyOrigin)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Authorization", "Content-Type");
    });
});

// ===================== RATE LIMITING =====================
// Giới hạn đăng nhập/làm mới phiên theo IP (chống dò mật khẩu, spam refresh). Số request/phút cấu hình ở RateLimit:AuthPerMinute.
var authPerMinute = builder.Configuration.GetValue<int>("RateLimit:AuthPerMinute", 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            Message = "Quá nhiều yêu cầu đăng nhập hoặc làm mới phiên. Vui lòng thử lại sau ít phút.",
            ErrorCode = "RATE_LIMIT_EXCEEDED"
        }, cancellationToken: token);
    };
    options.AddPolicy(LeaveManage.Constants.RateLimitPolicies.Auth, httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = authPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ===================== SWAGGER CONFIGURATION =====================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LeaveManage API",
        Version = "v1.0.0",
        Description = "Back-end quản lý ngày nghỉ của nhân sự — Nhóm 12"
    });

    // Enable XML Comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add the Authorize dialog for JWT Bearer tokens.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste the JWT returned by POST /api/users/login.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Tag descriptions for grouping endpoints
    c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
    c.OrderActionsBy(api => api.RelativePath);
});

var app = builder.Build();

// ===================== MIDDLEWARE =====================

app.UseRequestLogging();
app.UseExceptionHandling();
// Header bảo mật cho giao diện web (wwwroot). Swagger UI cần script inline nên không áp CSP cho /swagger.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    if (!context.Request.Path.StartsWithSegments("/swagger"))
        headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

// Giao diện web (HTML/CSS/JS thuần + Bootstrap) phục vụ từ wwwroot, cùng origin với API
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // Luôn hỏi lại server (ETag) để giao diện mới có hiệu lực ngay, không bị kẹt bản JS cũ trong cache
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});

// Swagger chỉ bật ở Development (không công khai tài liệu API ở Production)
if (app.Environment.IsDevelopment())
{
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LeaveManage API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "LeaveManage API";
    c.DefaultModelsExpandDepth(-1); // Hide schemas section
    c.DefaultModelsExpandDepth(2);
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    c.EnableDeepLinking();
    c.EnableFilter();
    c.EnableValidator();
    c.SupportedSubmitMethods(Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Get,
                              Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Post,
                              Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Put,
                              Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Delete);
    c.InjectStylesheet("/swagger-ui/custom.css");
    c.InjectJavascript("/swagger-ui/custom.js");
});
}

app.UseCors("Frontend");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ===================== DATABASE INITIALIZATION =====================
// Development: tự áp dụng migration và seed dữ liệu demo. Production: chạy migration chủ động (dotnet ef database update).
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Seed:Enabled"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    if (builder.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        await DbSeeder.SeedAsync(
            dbContext,
            scope.ServiceProvider.GetRequiredService<IPasswordService>(),
            builder.Configuration,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder"));
    }
    Log.Information("Database {Database} sẵn sàng.", dbContext.Database.GetDbConnection().Database);
}

app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Ứng dụng dừng bất thường khi khởi động.");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Khai báo partial để project test (WebApplicationFactory) tham chiếu được Program.</summary>
public partial class Program { }
