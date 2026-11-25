# DEFENSE Q&A — 20 câu hỏi bảo vệ (song ngữ Anh – Việt)

Mọi câu trả lời dựa trên mã nguồn thật của LeaveManage. Mỗi câu ghi file cần mở khi giảng viên yêu cầu chỉ vào code.
Nhãn kiến thức:
- **[S1–S11]** đã học trên lớp;
- **[S12–S15]** buổi cuối;
- **[Ngoài slide]** nhóm tự tìm hiểu, phải giải thích được.

---

### 1. Kiến trúc của dự án là gì? / What is the project's architecture?
**EN:** A layered Web API: Controller → Service → Repository → EF Core DbContext. Controllers only translate HTTP. Services hold business rules and ownership checks. Repositories are the only classes that touch `AppDbContext`. Everything is wired through dependency injection in `Program.cs`.
**VI:** Web API 3 lớp: Controller → Service → Repository → DbContext.
- Controller chỉ xử lý HTTP.
- Service chứa nghiệp vụ và kiểm tra quyền sở hữu.
- Repository là nơi duy nhất dùng `AppDbContext`.
- Tất cả được nối với nhau bằng DI trong `Program.cs`.

**Code:** [Program.cs](../Program.cs) (các dòng `AddScoped`), [Controllers/LeaveRequestsController.cs](../Controllers/LeaveRequestsController.cs). **[S2, S7]**

### 2. Vì sao Repository/Service là `AddScoped` mà `PasswordService` là `AddSingleton`? / Why Scoped vs Singleton?
**EN:** `AppDbContext` is scoped (one per HTTP request), so anything that depends on it must also be scoped. `PasswordService` has no state and no dependencies, so one shared instance is safe.
**VI:** DbContext sống theo từng request (Scoped), nên Repository và Service dùng nó cũng phải Scoped. `PasswordService` không giữ trạng thái, không phụ thuộc gì, nên dùng chung một instance (Singleton) là an toàn.
**[S2]**

### 3. Mật khẩu được lưu thế nào? / How are passwords stored?
**EN:** Never in plain text. `PasswordService.CreateHash` uses `HMACSHA512` with a random key that acts as the salt. We store `PasswordHash` and `PasswordSalt`. `Verify` recomputes the hash and compares it with `CryptographicOperations.FixedTimeEquals` to avoid timing attacks. LTW22 used to have a second mechanism (SHA-256 with a fixed salt "LTW22_Salt" on `Employee`); we removed it so there is only one password mechanism.
**VI:** Không lưu mật khẩu gốc.
- Dùng HMACSHA512, khóa ngẫu nhiên đóng vai trò salt riêng cho từng tài khoản.
- Khi đăng nhập, tính lại hash và so sánh bằng hàm so sánh thời gian cố định.
- LTW22 cũ có thêm cơ chế thứ hai (SHA-256, salt cố định) trên Employee. Nhóm đã bỏ để chỉ còn một cơ chế.

**Code:** [Services/PasswordService.cs](../Services/PasswordService.cs). **[S8]** (`FixedTimeEquals`: **[Ngoài slide]**)

### 4. JWT gồm những gì, ký bằng gì, kiểm tra ra sao? / What is inside the JWT and how is it validated?
**EN:** Claims: user id, username, role, department, age, jti, and `EmployeeId` when the account is linked. It is signed with HS256 using `Jwt:Key`. The server validates issuer, audience, lifetime (ClockSkew = 0) and signature.
**VI:**
- **Claims:** Id, username, role, phòng ban, tuổi, jti, và `EmployeeId` nếu tài khoản gắn nhân viên.
- **Ký:** HS256 bằng `Jwt:Key`.
- **Server kiểm tra:** issuer, audience, hạn dùng (không cho lệch giờ) và chữ ký.

**Code:** [Services/AuthService.cs](../Services/AuthService.cs) `GenerateJwtToken`, [Program.cs](../Program.cs) `AddJwtBearer`. **[S8]**

### 5. JWT Key được lưu ở đâu? / Where is the signing key stored?
**EN:** Not in the repository. In Development it comes from **User Secrets** (`dotnet user-secrets set "Jwt:Key" ...`). In production it would come from an environment variable (`Jwt__Key`). `Program.cs` refuses to start if the key is missing or shorter than 32 bytes.
**VI:** Không nằm trong repository.
- Development: lấy từ **User Secrets**.
- Production: lấy từ biến môi trường `Jwt__Key`.
- Thiếu key hoặc key ngắn hơn 32 byte thì ứng dụng dừng ngay khi khởi động.

**[S13]**

### 6. Refresh Token hoạt động thế nào? / How does the refresh token work?
**EN:** At login we generate 64 random bytes and return them to the client. The database stores only their SHA-256 hash and an expiry date. `/refresh` looks up the hash, checks expiry and that the account is active, then **rotates**: it issues a new pair and overwrites the stored hash, so an old refresh token cannot be reused. `/logout` clears the hash.
**VI:**
- Đăng nhập: tạo 64 byte ngẫu nhiên, trả cho client. Database chỉ lưu bản băm SHA-256 và hạn dùng.
- `/refresh`: tìm theo bản băm, kiểm tra hạn và tài khoản còn hoạt động, rồi phát cặp token mới và **ghi đè** bản băm (xoay vòng), nên token cũ dùng lại sẽ bị 401.
- `/logout`: xóa bản băm.

**Code:** [Services/AuthService.cs](../Services/AuthService.cs). **[S8]** (lưu bản băm + xoay vòng: mở rộng)

### 7. Phân quyền dùng những cách nào? / Which authorization techniques are used?
**EN:**
- **Role-based:** `AdminOnly` (`RequireRole`) and `CanApproveLeave` (Admin or Manager).
- **Claims-based:** `HasEmployee` (`RequireClaim("EmployeeId")`) and `MustBeHR`.
- **Custom requirement:** `MinimumAge18` (from LTW22).
- **Resource-based ownership checks** inside services, e.g. only the owner may submit, only the direct manager may approve.

**VI:**
- **Theo role:** `AdminOnly` dùng `RequireRole`; `CanApproveLeave` cho Admin hoặc Manager.
- **Theo claim:** `HasEmployee`, `MustBeHR`.
- **Requirement tự viết:** `MinimumAge18` (có từ LTW22).
- **Kiểm tra quyền sở hữu trong Service:** chỉ chủ đơn mới gửi duyệt, chỉ quản lý trực tiếp mới duyệt.

**Code:** [Program.cs](../Program.cs) `AddAuthorization`, [Services/LeaveRequestService.cs](../Services/LeaveRequestService.cs) `EnsureOwner`. **[S9]**

### 8. Làm sao User không xem được dữ liệu người khác? / How do you prevent users from seeing other people's data?
**EN:** Every query goes through a `DataScope` built from the JWT. A User sees only their own `EmployeeId`. A Manager also sees employees whose `ManagerId` equals theirs. Admin sees everything. A record outside the scope returns **404**, not 403, so an attacker cannot even learn that it exists.
**VI:** Mọi truy vấn đi qua `DataScope` tạo từ JWT:
- User: chỉ `EmployeeId` của mình;
- Manager: thêm nhân viên có `ManagerId` là mình;
- Admin: tất cả.

Bản ghi ngoài phạm vi trả **404** chứ không phải 403, để không lộ việc bản ghi đó có tồn tại.

**Code:** [Services/CallerContext.cs](../Services/CallerContext.cs), [Repositories/EmployeeRepository.cs](../Repositories/EmployeeRepository.cs) `GetPagedAsync`. **[S9]** + thiết kế của nhóm

### 9. Luồng trạng thái của đơn nghỉ? / What is the leave request lifecycle?
**EN:**
- Draft → (submit) → Pending → (approve) Approved, or (reject) Rejected.
- Draft, Pending, and Approved-not-yet-started → (cancel) Cancelled.
- Any other transition returns 409 with code `INVALID_STATUS` or `ALREADY_PROCESSED`.
- `Expired` exists in the enum but no background job sets it yet; this is documented as a limitation.

**VI:**
- Nháp → Gửi duyệt → Chờ duyệt → Đã duyệt hoặc Từ chối.
- Nháp, Chờ duyệt, Đã duyệt (chưa đến ngày nghỉ) → Hủy.
- Chuyển trạng thái sai thì trả 409.
- `Expired` có trong enum nhưng **chưa có tác vụ tự động**; nhóm ghi rõ là giới hạn.

**Code:** [Services/LeaveRequestService.cs](../Services/LeaveRequestService.cs).

### 10. Số ngày nghỉ được tính thế nào? / How are leave days counted?
**EN:** `LeaveDayCalculator.CountWorkingDays` counts Monday–Friday between start and end inclusive. Weekends are free; public holidays are not handled yet. A request must not span two years, because balances are per year.
**VI:** Đếm Thứ 2 – Thứ 6, tính cả ngày đầu và cuối. Thứ 7 và Chủ nhật không trừ phép; chưa trừ ngày lễ. Đơn không được kéo sang năm sau vì số dư tính theo năm.
Có unit test với 4 trường hợp.

**Code:** [Services/LeaveDayCalculator.cs](../Services/LeaveDayCalculator.cs).

### 11. Số dư phép thay đổi ra sao? / How does the balance change?
**EN:** Remaining = Initial − Used − Pending.
- Submit reserves `PendingDays`.
- Approve moves the days from Pending to Used.
- Reject releases Pending.
- Cancelling a Pending request releases Pending; cancelling an Approved request returns Used.

Only leave types with `AllowsDeduction` touch the balance. A database check constraint prevents negative numbers.
**VI:** Còn lại = Được cấp − Đã dùng − Đang chờ.
- Gửi duyệt: giữ chỗ (`PendingDays`).
- Duyệt: chuyển từ Đang chờ sang Đã dùng.
- Từ chối: trả lại phần Đang chờ.
- Hủy đơn Chờ duyệt: trả Đang chờ. Hủy đơn Đã duyệt: trả Đã dùng.

Chỉ loại nghỉ có `AllowsDeduction` mới động đến số dư. Check constraint trong database chặn số âm.

**Code:** [Services/LeaveRequestService.cs](../Services/LeaveRequestService.cs) `SubmitAsync`, `DecideAsync`, `CancelAsync`.

### 12. Hai quản lý bấm duyệt cùng lúc thì sao? / What if two approvers click at the same time?
**EN:** Approve runs in a transaction that first executes `SELECT ... WITH (UPDLOCK, ROWLOCK)` on the employee row. The second request waits until the first commits, then re-reads the request, sees it is no longer Pending, and gets 409. The balance is deducted once. As a last guard, `LeaveApprovals.LeaveRequestId` is unique in the database. An integration test fires two approvals concurrently and asserts exactly one 200 and one 409.
**VI:** Thao tác duyệt chạy trong transaction và trước hết khóa dòng nhân viên bằng `UPDLOCK`.
1. Request thứ hai phải chờ request thứ nhất commit.
2. Sau đó nó đọc lại đơn, thấy đơn không còn Chờ duyệt, và trả 409.
3. Số dư chỉ bị trừ một lần.

Lớp chặn cuối là unique index trên `LeaveRequestId`. Có test chạy 2 lệnh duyệt song song: đúng 1 lần 200 và 1 lần 409.

**Code:** [Repositories/LeaveRepository.cs](../Repositories/LeaveRepository.cs) `LockEmployeeAsync`, `ExecuteInTransactionAsync`. **[Ngoài slide]**

### 13. Làm sao phát hiện đơn trùng ngày? / How do you detect overlapping requests?
**EN:** Two ranges [s1, e1] and [s2, e2] overlap when `s1 <= e2 && s2 <= e1`. `HasOverlapAsync` applies this to the employee's Pending and Approved requests, excluding the request itself. It runs inside the same lock, so two overlapping submits sent at once cannot both pass (tested).
**VI:** Hai khoảng giao nhau khi `bắt đầu 1 ≤ kết thúc 2` và `bắt đầu 2 ≤ kết thúc 1`. Hàm kiểm tra các đơn Chờ duyệt và Đã duyệt của cùng nhân viên, bỏ qua chính đơn đang xét. Hàm chạy trong cùng khóa, nên 2 lần gửi trùng ngày cùng lúc chỉ 1 lần thành công (có test).

**Code:** [Repositories/LeaveRepository.cs](../Repositories/LeaveRepository.cs) `HasOverlapAsync`.

### 14. Lỗi được xử lý tập trung thế nào? / How are errors handled globally?
**EN:** Services throw `AppException` subclasses: `BadRequest`, `Forbidden`, `NotFound`, `Conflict`. `ExceptionHandlingMiddleware` maps them to a JSON body with `statusCode`, `message`, `errorCode` and `traceId`. EF exceptions are also mapped:
- `DbUpdateConcurrencyException` and unique-index violations (2601, 2627) → 409.
- Unknown errors → 500 with a generic message. Stack traces are shown only in Development.

**VI:** Service ném `AppException` (400/403/404/409). Middleware chuyển thành JSON thống nhất có `traceId`. Lỗi EF cũng được chuyển:
- vi phạm unique hoặc xung đột đồng thời → 409;
- lỗi lạ → 500 với thông báo chung, chỉ Development mới hiện chi tiết.

**Code:** [Middleware/ExceptionHandlingMiddleware.cs](../Middleware/ExceptionHandlingMiddleware.cs), [Exceptions/AppException.cs](../Exceptions/AppException.cs). **[S12]**

### 15. Validation nằm ở đâu? / Where is validation done?
**EN:** Two layers.
1. **Data Annotations on DTOs** (`[Required]`, `[Range]`, `[StringLength]`, `[Phone]`). `[ApiController]` returns 400 automatically when they fail.
2. **Business validation in services:** date order, notice days, weekend-only range, balance.

The database adds unique and check constraints as a final safety net.
**VI:** Hai lớp:
1. **Data Annotations trên DTO:** `[ApiController]` tự trả 400 khi sai.
2. **Kiểm tra nghiệp vụ trong Service:** thứ tự ngày, báo trước, chỉ toàn ngày cuối tuần, số dư.

Database thêm unique và check constraint làm lớp cuối.

**Code:** [DTOs/Requests/LeaveRequests.cs](../DTOs/Requests/LeaveRequests.cs). **[S4]**

### 16. Phân trang, tìm kiếm, lọc làm ở đâu? / Where are pagination, search and filtering?
**EN:** On the server with `IQueryable`:
1. Apply `Where` for the scope, search and filters.
2. Run `CountAsync` for the total.
3. Run `Skip((page-1)*pageSize).Take(pageSize)`.

DTO attributes limit `pageSize`, and `UserQueryParameters` clamps `PageNumber` to at least 1. The response contains `totalCount`, `totalPages`, `hasNextPage`.
**VI:** Làm phía server với `IQueryable`:
1. Lọc theo phạm vi, từ khóa và bộ lọc.
2. Đếm tổng số bản ghi.
3. `Skip/Take` để lấy đúng trang.

`pageSize` bị giới hạn bằng attribute; `PageNumber` tối thiểu là 1 (lỗi audit đã sửa).

**Code:** [Repositories/LeaveRepository.cs](../Repositories/LeaveRepository.cs) `GetRequestsPagedAsync`. **[S11]**

### 17. Vì sao dùng `AsNoTracking` và khi nào thì không? / Why AsNoTracking?
**EN:** Read-only queries use `AsNoTracking` to skip change tracking, which is faster. Queries whose entities we modify (`GetRequestTrackedAsync`, `GetBalanceTrackedAsync`) are tracked so `SaveChangesAsync` writes the changes. Before locking we read only the owner id (a projection, not tracked); after taking the lock we load the tracked entity, so we always see the latest committed values.
**VI:**
- Truy vấn chỉ đọc dùng `AsNoTracking` cho nhanh.
- Truy vấn cần sửa thì để EF theo dõi, để `SaveChanges` ghi được.
- Trước khi khóa chỉ đọc Id chủ đơn (không tracking). Sau khi khóa mới nạp entity, nên luôn thấy dữ liệu mới nhất.

**[Ngoài slide]**

### 18. Quan hệ giữa User và Employee? / How are User and Employee related?
**EN:** One employee has zero or one account (1-0..1). `User.EmployeeId` is nullable with a **filtered unique index** (`WHERE EmployeeId IS NOT NULL`), and the service also checks that the employee exists and is not already linked (409). The JWT carries `EmployeeId`, so the API knows "who am I" without trusting any id sent by the client.
**VI:** Một nhân viên có 0 hoặc 1 tài khoản.
- `User.EmployeeId` cho phép null, có **unique index có lọc**.
- Service kiểm tra thêm: nhân viên phải tồn tại và chưa có tài khoản (nếu đã có → 409).
- JWT mang `EmployeeId`, nên API biết "tôi là ai" mà không tin Id do client gửi lên.

**Code:** [Data/AppDbContext.cs](../Data/AppDbContext.cs), [Services/UserService.cs](../Services/UserService.cs). **[S6]** (filtered index: **[Ngoài slide]**)

### 19. CORS và bảo mật giao diện? / CORS and front-end security?
**EN:**
- **CORS:** only the origins listed in `Cors:AllowedOrigins`. LTW22's AllowAll policy was removed. The UI is served from the same origin, so it doesn't need CORS.
- **Token storage:** `sessionStorage`, sent as `Authorization: Bearer`. On 401 the client tries one refresh before logging out.
- **XSS:** all data is rendered with `textContent`, never `innerHTML`, and a Content-Security-Policy header blocks inline scripts.
- **Registration:** public registration is disabled; only Admin creates accounts.

**VI:**
- **CORS:** chỉ cho origin cấu hình sẵn (đã bỏ AllowAll của LTW22). Giao diện chạy cùng origin nên không cần CORS.
- **Lưu token:** trong `sessionStorage`, gửi qua header Bearer. Gặp 401 thì tự refresh một lần.
- **XSS:** hiển thị dữ liệu bằng `textContent`, có header CSP.
- **Đăng ký:** không cho đăng ký công khai; chỉ Admin cấp tài khoản.

**Code:** [Program.cs](../Program.cs), [wwwroot/js/core/api.js](../wwwroot/js/core/api.js). **[S13, S14]**

### 20. Kiểm thử những gì và kết quả? / What did you test and what were the results?
**EN:** 20 xUnit integration tests with `WebApplicationFactory` against a real SQL Server database (`LeaveManage_Test`, recreated each run and guarded by name). They cover:
- login and refresh rotation; 401/403;
- account linking and locking;
- data scope;
- the full workflow with balance math; overlap; insufficient balance; ownership;
- **two concurrency tests**.

Plus a 28-request Postman collection (run with Newman) and 25 automated UI checks in Chrome. All results are recorded in TEST_RESULTS.md.
**VI:**
- 20 integration test chạy trên SQL Server thật, database riêng `LeaveManage_Test`, có kiểm tra tên trước khi xóa. Phạm vi gồm đăng nhập/refresh, 401/403, phạm vi dữ liệu, toàn bộ luồng đơn kèm số dư, trùng ngày, thiếu số dư, quyền sở hữu, và 2 test đồng thời.
- Postman 28 request.
- 25 kiểm tra giao diện trên Chrome.

Kết quả thật ghi ở [docs/TEST_RESULTS.md](TEST_RESULTS.md). **[S15]** (xUnit/WebApplicationFactory: **[Ngoài slide]**)
