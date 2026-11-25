# Đối chiếu kiến thức môn học ↔ mã nguồn LeaveManage

Nhãn:
- **Đã học (S1–S11):** kiến thức các buổi đã học trên lớp.
- **Buổi cuối (S12–S15):** kiến thức các buổi cuối.
- **Ngoài slide:** nhóm tự tìm hiểu thêm, phải tự giải thích được khi bảo vệ.

| Buổi | Kiến thức | Áp dụng ở đâu trong LeaveManage |
|---|---|---|
| S1 | .NET, cấu trúc project, `Program.cs` | [LeaveManage.csproj](../LeaveManage.csproj), [Program.cs](../Program.cs) |
| S2 | ASP.NET Core, middleware pipeline, DI (`AddScoped`, `AddSingleton`) | [Program.cs](../Program.cs): đăng ký Repository/Service, thứ tự `UseAuthentication` → `UseAuthorization` |
| S3 | Controller, routing, HTTP method, status code | [Controllers/LeaveRequestsController.cs](../Controllers/LeaveRequestsController.cs) (`[Route("api/leave-requests")]`, `CreatedAtAction` → 201, `NoContent` → 204) |
| S4 | DTO, Data Annotations, Custom Validation | [DTOs/Requests/LeaveRequests.cs](../DTOs/Requests/LeaveRequests.cs); `MustBeEvenAttribute` (từ LTW22) |
| S5 | EF Core, DbContext, Migration | [Data/AppDbContext.cs](../Data/AppDbContext.cs), [Migrations/](../Migrations/) (`InitialCreate`) |
| S6 | CRUD, quan hệ 1-n / n-n, `Include` | Department → Employee (1-n); Employee ↔ LeaveType qua LeaveBalance (n-n có thuộc tính); `Include(r => r.Employee)` |
| S7 | Kiến trúc nhiều lớp, Repository + Service | `Repositories/*`, `Services/*`; Controller không dùng DbContext |
| S8 | Xác thực: hash mật khẩu HMAC, JWT, Refresh Token | [Services/PasswordService.cs](../Services/PasswordService.cs), [Services/AuthService.cs](../Services/AuthService.cs) |
| S9 | Phân quyền: Role, Claim, Policy, Custom Requirement | [Program.cs](../Program.cs) `AddAuthorization` (`AdminOnly`, `CanApproveLeave`, `HasEmployee`, `MustBeHR`, `MinimumAge18`) |
| S10 | Upload file, `IOptions<T>` | `FilesController` + `FileUploadOptions` (giữ từ LTW22) |
| S11 | Phân trang, lọc, tìm kiếm (`Skip/Take`), `record` | `GetRequestsPagedAsync`, `EmployeeRepository.GetPagedAsync`; `CallerContext`/`DataScope` là `record` |
| S12 | Global Exception Middleware, ILogger, logging | [Middleware/ExceptionHandlingMiddleware.cs](../Middleware/ExceptionHandlingMiddleware.cs), `RequestLoggingMiddleware`, `ILogger<LeaveRequestService>` |
| S13 | Swagger nâng cao (Bearer), CORS, User Secrets | `AddSwaggerGen` + `AddSecurityDefinition`; CORS theo `Cors:AllowedOrigins`; `Jwt:Key` trong user-secrets |
| S14 | Tích hợp frontend, lưu JWT, header Bearer, checklist dự án | [wwwroot/js/core/api.js](../wwwroot/js/core/api.js), [wwwroot/js/core/session.js](../wwwroot/js/core/session.js) |
| S15 | Kiểm thử Happy Path / lỗi / phân quyền, Postman, chuẩn bị bảo vệ | [tests/](../tests/), [postman/](../postman/), [docs/TEST_RESULTS.md](TEST_RESULTS.md), [docs/DEMO_SCRIPT.md](DEMO_SCRIPT.md) |

## Ngoài slide (nhóm tự tìm hiểu)

| Kỹ thuật | Ở đâu | Vì sao cần |
|---|---|---|
| Transaction + khóa `UPDLOCK` | `LeaveRepository.ExecuteInTransactionAsync`, `LockEmployeeAsync` | Chống duyệt 2 lần và trừ số dư 2 lần khi có thao tác đồng thời |
| Fluent API: `HasIndex(...).IsUnique().HasFilter(...)`, `HasCheckConstraint`, `OnDelete(Restrict)` | `AppDbContext.OnModelCreating` | Ràng buộc toàn vẹn ở mức database |
| `AsNoTracking` | các Repository | Truy vấn chỉ đọc nhanh hơn |
| `CryptographicOperations.FixedTimeEquals` | `PasswordService.Verify` | Chống tấn công đo thời gian |
| Repository generic `CatalogRepository<T>` | [Repositories/CatalogRepository.cs](../Repositories/CatalogRepository.cs) | Dùng chung cho 3 danh mục |
| Serilog | `Program.cs` | Ghi log có cấu trúc |
| xUnit + `WebApplicationFactory` | [tests/LeaveManage.Tests](../tests/LeaveManage.Tests) | Integration test trên SQL Server thật |
| Content-Security-Policy | `Program.cs` | Chống XSS cho giao diện |

## Đối chiếu checklist Buổi 14 (trang 8–9)

| Mục checklist | Trạng thái |
|---|---|
| Quan hệ 1-n, n-n | Có (xem bảng trên) |
| EF Core Migration | Có (`InitialCreate`) |
| RESTful routing, đúng HTTP method | Có |
| Validate bằng Data Annotations | Có |
| Login sinh JWT | Có (kèm Refresh Token) |
| Chặn `[Authorize]` | Có; chỉ `login` và `refresh` là ẩn danh |
| Phân quyền Admin/User | Có Admin / Manager / User |
| DI, Repository, Service | Có |
| Global Exception Middleware | Có |
| Pagination cho danh sách dài | Có cho đơn nghỉ, nhân viên, tài khoản, lịch sử duyệt. Danh mục và số dư là danh sách ngắn, không phân trang (số dư giới hạn 500 dòng). |
