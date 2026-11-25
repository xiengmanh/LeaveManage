# Hướng dẫn đọc hiểu mã nguồn LeaveManage

Mục tiêu: trước giờ bảo vệ, mỗi thành viên tự giải thích được một request đi từ trình duyệt tới database và quay về như thế nào. Đọc theo thứ tự dưới đây, mỗi bước mở đúng file được nêu.

## Bước 1 — Khởi động ứng dụng: [Program.cs](../Program.cs)

Thứ tự trong file:
1. **Serilog:** ghi log.
2. **`AddControllers`:** dùng Controller.
3. **`AddDbContext`:** chuỗi kết nối lấy từ `appsettings.Development.json` (database `LeaveManageDb`).
4. **Đăng ký DI:**
   - `AddScoped<ILeaveRepository, LeaveRepository>()` nghĩa là mỗi request HTTP có một instance;
   - `AddSingleton<IPasswordService, PasswordService>()` nghĩa là cả ứng dụng dùng chung một instance.
5. **JWT:** đọc `Jwt:Key` từ user-secrets, cấu hình `TokenValidationParameters`.
6. **Policy phân quyền:** `AdminOnly`, `CanApproveLeave`, `HasEmployee`...
7. **CORS:** chỉ origin cấu hình sẵn.
8. **Swagger:** chỉ ở Development.
9. **Pipeline:**
   1. `UseRequestLogging`
   2. `UseExceptionHandling`
   3. header bảo mật
   4. `UseDefaultFiles` / `UseStaticFiles` (giao diện)
   5. `UseCors`
   6. `UseAuthentication`
   7. `UseAuthorization`
   8. `MapControllers`
10. **Development:** `Database.MigrateAsync()` rồi `DbSeeder.SeedAsync` (tạo tài khoản demo).

## Bước 2 — Dữ liệu: [Models/AllModels.cs](../Models/AllModels.cs), [Models/User.cs](../Models/User.cs), [Data/AppDbContext.cs](../Data/AppDbContext.cs)

- Mở `AppDbContext.OnModelCreating` và tìm từng dòng `HasIndex(...).IsUnique()`, `HasCheckConstraint`, `OnDelete(DeleteBehavior.Restrict)`. Giải thích được vì sao mỗi ràng buộc tồn tại.
- `HasData` là dữ liệu mẫu nằm trong migration: phòng ban, chức vụ, loại nghỉ, 6 nhân viên, số dư 2026.
- Tài khoản đăng nhập **không** nằm trong `HasData`, vì mật khẩu không được ghi vào mã nguồn. Tài khoản được tạo lúc chạy bởi `DbSeeder` với mật khẩu lấy từ user-secrets.

## Bước 3 — Đăng nhập: `POST /api/Users/login`

1. [Controllers/UsersController.cs](../Controllers/UsersController.cs) `Login` gọi `IAuthService.LoginAsync`.
2. [Services/AuthService.cs](../Services/AuthService.cs):
   1. Tìm user theo username (qua `AuthRepository`).
   2. Kiểm tra `IsActive` và `PasswordService.Verify`.
   3. Tạo refresh token ngẫu nhiên. `TryStoreRefreshTokenAsync` lưu **bản băm** vào DB bằng `UPDATE` có điều kiện: tài khoản còn hoạt động và `TokenVersion` chưa đổi.
   4. Tạo JWT (claims: id, username, role, `EmployeeId`, **`TokenVersion`**).
3. Trả về `{ token, refreshToken, expiresAt, username, role, employeeId }`.

Bài tập tự kiểm tra: dán token vào https://jwt.io (chỉ dùng token demo) và chỉ ra claim `EmployeeId` và `TokenVersion`.

## Bước 3b — Thu hồi token: `TokenVersion` (sửa lỗi audit P1-02)

JWT là token không trạng thái: chữ ký đúng và còn hạn thì thư viện JWT chấp nhận. Vì vậy:
- [Program.cs](../Program.cs) gắn sự kiện `OnTokenValidated`. Sau khi chữ ký và hạn hợp lệ, gọi `AuthService.IsAccessTokenCurrentAsync`.
- Hàm này đọc trạng thái **hiện tại** của tài khoản (`AuthRepository.GetAuthStateAsync`, 1 truy vấn theo khóa chính) và từ chối (401) khi:
  - tài khoản không còn tồn tại hoặc đã bị khóa;
  - `TokenVersion` trong token khác `TokenVersion` trong DB;
  - Role hoặc EmployeeId trong token khác dữ liệu hiện tại.
- `TokenVersion` tăng (`AuthRepository.RevokeSessionsAsync`, một câu `UPDATE ... SET TokenVersion = TokenVersion + 1`) khi: đăng xuất, khóa tài khoản, đổi role, đổi nhân viên liên kết, đặt lại mật khẩu. Tăng version cũng xóa refresh token.
- Đăng nhập lại **không** làm token cũ sống lại, vì token cũ vẫn mang version cũ.
- `UserService.UpdateAsync` **không** gọi `DbSet.Update` (lệnh đó đánh dấu mọi cột). Entity đang được tracking nên `SaveChanges` chỉ ghi cột đã đổi, không ghi đè `TokenVersion` hay refresh token mới hơn.

## Bước 3c — Refresh nguyên tử: `AuthRepository.RotateRefreshTokenAsync` (sửa lỗi audit P1-03)

Bản cũ đọc bản ghi rồi mới ghi hash mới, nên 5 request cùng token có thể cùng đọc thấy hash cũ và cùng thắng. Bản mới:
1. Mở transaction.
2. Chạy **một câu** `UPDATE Users SET RefreshTokenHash = @new ... WHERE RefreshTokenHash = @old AND RefreshTokenExpiresAt > @now AND IsActive = 1`.
3. SQL Server khóa dòng khi cập nhật. Request đến sau phải chờ, rồi đánh giá lại điều kiện trên dữ liệu mới: hash cũ không còn khớp → 0 dòng → **401**.
4. Request thắng (1 dòng) đọc lại thông tin tài khoản trong cùng transaction rồi mới phát JWT.

Kết hợp với Bước 3b: nếu logout hoặc khóa xảy ra ngay sau đó, `TokenVersion` tăng nên token vừa phát cũng hết hiệu lực.

## Bước 4 — Một request có bảo vệ: `POST /api/leave-requests/{id}/approve`

1. Middleware JWT kiểm tra chữ ký và hạn của token. Không có token hoặc token sai → **401**.
2. `[Authorize(Policy = CanApproveLeave)]` kiểm tra role. Role User → **403**.
3. [Controllers/LeaveRequestsController.cs](../Controllers/LeaveRequestsController.cs) `Approve` tạo `CallerContext.From(User)` (đọc claim) rồi gọi Service.
4. [Services/LeaveRequestService.cs](../Services/LeaveRequestService.cs) `DecideAsync`:
   1. `caller.RequireEmployeeId()`: tài khoản phải gắn nhân viên.
   2. Lấy Id chủ đơn (không tracking).
   3. `ExecuteInTransactionAsync`: mở transaction, rồi:
      1. `LockEmployeeAsync(ownerId)`: khóa dòng nhân viên bằng `UPDLOCK`.
      2. Đọc lại đơn (tracking).
      3. `EnsureVisible`: ngoài phạm vi → 404.
      4. Tự duyệt đơn của mình → 403.
      5. Manager mà không phải quản lý trực tiếp → 403.
      6. Đơn không còn Pending → 409.
      7. Cập nhật số dư: `PendingDays -= n`, `UsedDays += n`.
      8. Đổi trạng thái và thêm bản ghi `LeaveApproval`.
      9. `SaveChangesAsync`, rồi Commit.
   4. Trả về chi tiết đơn.
5. Nếu Service ném `ConflictException`, [Middleware/ExceptionHandlingMiddleware.cs](../Middleware/ExceptionHandlingMiddleware.cs) chuyển thành JSON 409 kèm `errorCode` và `traceId`.

## Bước 4b — Sửa nháp: `UpdateDraftAsync` (sửa lỗi audit P1-01)

Bản cũ sửa đơn **không khóa**. Nếu "Sửa" và "Gửi duyệt" chạy cùng lúc, Submit đã giữ chỗ 1 ngày rồi lệnh sửa ghi đè thành 2 ngày, nên đơn 2 ngày nhưng `PendingDays` chỉ 1. Sau đó duyệt hoặc hủy đều lỗi.

Bản mới dùng **cùng giao thức** với mọi thao tác thay đổi đơn hoặc số dư:
1. Mở transaction.
2. `LockEmployeeAsync`.
3. **Sau khi có khóa** mới đọc đơn (tracking).
4. Kiểm tra chủ đơn và trạng thái Nháp.
5. Kiểm tra dữ liệu.
6. Lưu, rồi Commit.

Kết quả chỉ có 2 khả năng hợp lệ:
- **Sửa xong trước:** Gửi duyệt tính theo số ngày mới.
- **Gửi duyệt xong trước:** lệnh sửa thấy đơn đã Chờ duyệt → 409.

Ghi nhớ: khóa chỉ có tác dụng khi **mọi** thao tác ghi cùng lấy khóa đó.

## Bước 5 — Gửi duyệt và số dư: `SubmitAsync`

Đọc lần lượt các kiểm tra:
1. Chủ đơn.
2. Trạng thái Draft.
3. Nhân viên còn làm việc.
4. Loại nghỉ còn dùng.
5. Báo trước.
6. Trùng ngày (`HasOverlapAsync`).
7. Số dư (`RemainingDays`) hoặc giới hạn/năm.

Sau đó `PendingDays += n`, trạng thái chuyển thành Pending.

Đọc tiếp [Services/LeaveDayCalculator.cs](../Services/LeaveDayCalculator.cs) để biết cách đếm ngày.

## Bước 6 — Phạm vi dữ liệu: [Services/CallerContext.cs](../Services/CallerContext.cs)

- `DataScope.For(caller)`:
  - Admin → `AllEmployees = true`;
  - Manager → `EmployeeId` + `IncludeDirectReports`;
  - User → chỉ `EmployeeId`.
- Tìm chỗ dùng `scope.AllEmployees` trong [Repositories/LeaveRepository.cs](../Repositories/LeaveRepository.cs) và [Repositories/EmployeeRepository.cs](../Repositories/EmployeeRepository.cs). Đây là điều kiện `Where` được thêm vào truy vấn.

## Bước 7 — Phân trang: `GetRequestsPagedAsync`

`IQueryable` → các `Where` (phạm vi, bộ lọc, tìm kiếm) → `CountAsync()` lấy tổng → `Include` → `OrderByDescending` → `Skip(PagingLimits.Skip(page, pageSize)).Take(pageSize)`.

Giới hạn số trang ở [Constants/PagingLimits.cs](../Constants/PagingLimits.cs) (sửa lỗi audit P2-01):
- Số trang phải từ 1 đến 100.000. DTO kiểm tra bằng `[Range]`, `PagingLimits.Skip` kiểm tra lại và nhân bằng `checked`.
- Trước đây `page = 2147483647` làm `(page-1)*pageSize` tràn số int và trả 500. Bây giờ trả 400.

## Bước 8 — Giao diện: [wwwroot/js](../wwwroot/js)

- [core/api.js](../wwwroot/js/core/api.js):
  - gắn header `Authorization: Bearer`;
  - gặp 401 thì gọi `/api/users/refresh` một lần rồi gửi lại request;
  - nếu refresh cũng 401 (tài khoản bị khóa hoặc đã đăng xuất) thì xóa phiên và về trang đăng nhập;
  - đổi `PaginatedResponse` sang dạng bảng dùng.
- [core/session.js](../wwwroot/js/core/session.js): lưu token trong `sessionStorage`.
- [components/layout.js](../wwwroot/js/components/layout.js): menu theo role. Đây chỉ là tiện ích hiển thị; quyền thật do API kiểm tra.
- [pages/requests.js](../wwwroot/js/pages/requests.js): tạo, gửi, duyệt, từ chối, hủy đơn.

## Bước 9 — Kiểm thử: [tests/LeaveManage.Tests](../tests/LeaveManage.Tests)

- `ApiFixture`:
  1. Xóa và tạo lại **đúng** database `LeaveManage_Test` (có guard tên).
  2. Chạy API thật bằng `WebApplicationFactory<Program>`.
  3. Cấp key/mật khẩu riêng cho test.
- `Concurrency_TwoApproversAtOnce_OnlyOneSucceeds_BalanceDeductedOnce`: dùng `Task.WhenAll` gửi 2 lệnh duyệt cùng lúc.
- [RegressionTests.cs](../tests/LeaveManage.Tests/RegressionTests.cs): 15 test cho đúng các lỗi audit tìm ra:
  - sửa nháp đua với gửi duyệt (lặp 15 lượt);
  - JWT cũ sau khóa, logout, đổi quyền;
  - 8 refresh cùng lúc;
  - refresh đua với logout hoặc khóa;
  - số trang cực lớn.

  Cùng bộ test này chạy trên bản cũ `99955c9` thì fail 14/15.

## Câu hỏi tự kiểm tra

1. Nếu bỏ `LockEmployeeAsync`, test đồng thời nào có thể fail? Vì sao?
2. Vì sao User xem đơn của người khác nhận 404 mà không phải 403?
3. Vì sao chỉ lưu bản băm của refresh token?
4. Đơn từ Thứ 6 đến Thứ 2 tính mấy ngày?
5. Hủy đơn đã duyệt thì cột nào của `LeaveBalance` thay đổi?
6. Vì sao chỉ kiểm tra chữ ký JWT là chưa đủ khi khóa tài khoản? `TokenVersion` giải quyết thế nào?
7. Hai request refresh dùng cùng token: vì sao câu `UPDATE ... WHERE RefreshTokenHash = @old` chỉ cho 1 request thắng?
