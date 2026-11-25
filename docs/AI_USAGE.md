# Kê khai sử dụng AI

- **Công cụ:** Claude Code (Anthropic), dùng trong VS Code trên máy của nhóm.
- **Căn cứ:** theo xác nhận của nhóm ngày 24/11/2026, giảng viên cho phép sử dụng AI với điều kiện kê khai. Đây là xác nhận của nhóm; AI không tự giả định điều này.

## 1. Phân biệt nguồn gốc mã

| Nguồn | Nội dung |
|---|---|
| **Mã gốc LTW22** (nhóm viết trong các buổi lab) | 9 entity trong `Models/` (Employee, Department, Position, LeaveType, LeaveRequest, LeaveBalance, LeaveApproval, User, StoredFile); `EmployeeRepository/Service/Controller`; `UserRepository/Service`, `UsersController`; JWT + Refresh Token ban đầu; `MinimumAgeRequirement`; `MustBeEvenAttribute`; upload file (`FilesController`, `FileService`, `FileStorageService`, `FileUploadOptions`); `ExceptionHandlingMiddleware`, `RequestLoggingMiddleware`; cấu hình Swagger và `wwwroot/swagger-ui`; `ErrorsController`, `SystemController`, `ValidationController`; dữ liệu mẫu (phòng ban, chức vụ, loại nghỉ, 6 nhân viên) |
| **Tái sử dụng từ UniManage** (đồ án trước của cùng nhóm, AI hỗ trợ viết) | Giao diện: `wwwroot/lib` (Bootstrap 5.3, Bootstrap Icons), `css/app.css`, `js/core/dom.js`, `format.js`, `router.js`, `js/components/data-table.js`, `modal-form.js`, `confirm.js`, `toast.js`, `states.js` (đã chỉnh nhỏ cho LeaveManage). Mẫu thiết kế: transaction + khóa `UPDLOCK`, integration test với `WebApplicationFactory` trên database test có kiểm tra tên. |
| **AI thêm/sửa trong LeaveManage** | Xem mục 2 |

## 2. Phần AI thêm hoặc sửa (nhóm phải đọc hiểu trước khi bảo vệ)

**Thêm mới:**
- `Services/PasswordService.cs`, `Services/CallerContext.cs` (CallerContext, DataScope), `Services/LeaveDayCalculator.cs`.
- `Services/LeaveRequestService.cs`, `LeaveBalanceService.cs`, `DashboardService.cs`, `CatalogService.cs`.
- `Repositories/LeaveRepository.cs` (+ interface), `Repositories/CatalogRepository.cs`, `Models/ICatalogEntity.cs`.
- `Controllers/LeaveRequestsController.cs` (gồm LeaveApprovals, LeaveBalances, Dashboard), `Controllers/CatalogControllers.cs`.
- `DTOs/Requests/LeaveRequests.cs`, `DTOs/Responses/LeaveResponses.cs`.
- `Data/DbSeeder.cs`, migration `InitialCreate` mới.
- `tests/LeaveManage.Tests/*`, `postman/LeaveManage.postman_collection.json`.
- Các trang giao diện `wwwroot/js/pages/*`, `js/core/api.js`, `session.js`, `components/layout.js`, `ui.js`, `index.html`.

**Sửa:**
- `AuthService` (khóa tài khoản, claim EmployeeId, logout), `UserService` (gắn nhân viên, khóa, dùng PasswordService).
- `EmployeeService/Repository/Controller` (bỏ mật khẩu, phạm vi dữ liệu, chống vòng lặp quản lý).
- `AppDbContext` (khóa chính LeaveBalance, quan hệ User–Employee, unique, check constraint, seed).
- `ExceptionHandlingMiddleware` (409, errorCode).
- `Program.cs` (user-secrets, CORS, Swagger chỉ Development, policy mới, seed, header bảo mật).
- `appsettings*.json` (bỏ key và chuỗi kết nối khỏi file chung).

**Tài liệu:** README và toàn bộ `docs/*` do AI soạn từ mã nguồn thật, nhóm rà soát.

## 3. Cách nhóm kiểm soát chất lượng phần AI viết

- Mọi kết quả "PASS" trong [TEST_RESULTS.md](TEST_RESULTS.md) là kết quả chạy thật trên máy.
- AI không được sửa thư mục LTW22 gốc, UniManage, LeaveManagementApi hay tài liệu môn học.
- AI không được xóa hoặc ghi đè các database cũ.
- AI không được push hay deploy.
- Bí mật (JWT Key, mật khẩu demo) không nằm trong repository.
- Trước buổi bảo vệ, nhóm đọc [LEARNING_GUIDE.md](LEARNING_GUIDE.md) và [DEFENSE_QA.md](DEFENSE_QA.md), mở từng file được nhắc tới để tự giải thích.
