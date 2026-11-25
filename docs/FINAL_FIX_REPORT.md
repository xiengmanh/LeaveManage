# FINAL FIX REPORT — LeaveManage

- **Ngày:** 25/11/2026 (trước buổi bảo vệ 15:45).
- **Căn cứ:** [CODEX_FINAL_AUDIT.md](CODEX_FINAL_AUDIT.md) và [FINAL_EXAM_READINESS.md](FINAL_EXAM_READINESS.md), audit tại commit `99955c9`. Hai báo cáo được giữ nguyên.
- **Phạm vi:**
  - Đã sửa: 3 lỗi P1 và 1 lỗi P2.
  - Theo yêu cầu, **không** xử lý P3-01 và P3-02, không thêm tính năng, không đổi kiến trúc hay giao diện.

**Commit:**

| Commit | Nội dung |
|---|---|
| `99955c9` | Bản được audit |
| `fce70a4` | **Mã nguồn đóng băng mới**: sửa lỗi + regression test |
| Commit kế tiếp | Chỉ cập nhật tài liệu, ghi trong `git log` |

Không push, không deploy.

---

## P1-01 — Sửa nháp đồng thời với gửi duyệt làm lệch số dư

**Nguyên nhân:**
- `UpdateDraftAsync` đọc đơn, kiểm tra "Nháp" rồi lưu mà **không** dùng transaction và khóa nhân viên.
- `Submit`, `Approve`, `Cancel` đều khóa dòng Employee bằng `UPDLOCK`, nhưng khóa chỉ hiệu quả khi **mọi** thao tác ghi cùng lấy nó.
- Hậu quả khi sửa và gửi chạy cùng lúc: Submit giữ chỗ theo số ngày cũ (1), rồi lệnh sửa ghi đè TotalDays = 2. Đơn Chờ duyệt có TotalDays 2 nhưng PendingDays 1, sau đó duyệt hoặc hủy đều bị 409.

**Giải pháp:** `UpdateDraftAsync` dùng cùng giao thức với các thao tác khác:
1. `ExecuteInTransactionAsync`.
2. `LockEmployeeAsync`.
3. **Sau khi có khóa** mới đọc đơn (tracking).
4. Kiểm tra chủ đơn và trạng thái Nháp.
5. Kiểm tra lại toàn bộ dữ liệu (ngày, báo trước, loại nghỉ).
6. Lưu, rồi Commit.

Kết quả chỉ còn 2 khả năng hợp lệ:
- **Sửa xong trước:** gửi duyệt tính theo dữ liệu mới.
- **Gửi xong trước:** lệnh sửa nhận 409 `INVALID_STATUS`.

Không dùng try/catch để che lỗi. Kiến trúc Controller → Service → Repository giữ nguyên.

**File:** `Services/LeaveRequestService.cs`.

## P1-02 — JWT cũ vẫn dùng được sau khi khóa tài khoản / logout

**Nguyên nhân:**
- JwtBearer chỉ kiểm tra chữ ký, issuer, audience và hạn. Không kiểm tra trạng thái tài khoản hiện tại.
- Khóa tài khoản và logout chỉ xóa refresh token, nên access JWT (60 phút) vẫn đọc và duyệt được.

**Giải pháp (cơ chế token version, không quản lý đa phiên):**
- **Database:** thêm cột `User.TokenVersion` (migration `AddUserTokenVersion`, chỉ thêm cột `int NOT NULL DEFAULT 0`).
- **JWT:** mang claim `TokenVersion`.
- **Kiểm tra mỗi request:** `Program.cs` → `JwtBearerEvents.OnTokenValidated` gọi `AuthService.IsAccessTokenCurrentAsync`, hàm này đọc `AuthRepository.GetAuthStateAsync` (1 truy vấn theo khóa chính, không tracking). Token bị từ chối **401** khi:
  - tài khoản không tồn tại hoặc bị khóa;
  - `TokenVersion` lệch;
  - Role lệch;
  - EmployeeId lệch.
- **Tăng `TokenVersion` (nguyên tử, `UPDATE ... SET TokenVersion = TokenVersion + 1`, đồng thời xóa refresh token) khi:**
  - logout;
  - khóa tài khoản;
  - đổi role;
  - đổi nhân viên liên kết;
  - đặt lại mật khẩu.
- **Token cũ không sống lại:** đăng nhập lại hoặc mở khóa đều không làm token cũ dùng được, vì token cũ mang version cũ.
- **Lỗi phát hiện thêm khi sửa:** `UserService.UpdateAsync` gọi `DbSet.Update`, lệnh này đánh dấu **mọi cột** và có thể ghi đè `TokenVersion`/refresh token mới hơn bằng giá trị cũ. Đã bỏ lệnh đó; entity đang tracking nên `SaveChanges` chỉ ghi cột đã đổi.
- **Frontend:** khi 401 đã được xử lý (về trang đăng nhập), không ghi thêm `console.error` hay thông báo lỗi trang.

**File:**
- `Models/User.cs`, `Migrations/20261125051222_AddUserTokenVersion*.cs`, `Migrations/AppDbContextModelSnapshot.cs`
- `Constants/AuthorizationConstants.cs`
- `Repositories/IAuthRepository.cs`, `Repositories/AuthRepository.cs`
- `Services/IAuthService.cs`, `Services/AuthService.cs`, `Services/UserService.cs`
- `Program.cs`, `wwwroot/js/main.js`

## P1-03 — Refresh token rotation không nguyên tử

**Nguyên nhân:** đọc user theo hash cũ, rồi gán hash mới và `SaveChanges`. Hai giai đoạn này không nguyên tử, nên nhiều request cùng token cùng đọc thấy hash cũ và cùng được phát token. Audit đo được 2–4 request thắng.

**Giải pháp:**
- **Refresh:** `AuthRepository.RotateRefreshTokenAsync` chạy **một câu `UPDATE` có điều kiện** trong transaction:

  ```sql
  UPDATE Users SET RefreshTokenHash = @new, RefreshTokenExpiresAt = @exp
  WHERE RefreshTokenHash = @old AND RefreshTokenExpiresAt > @now AND IsActive = 1
  ```

  SQL Server khóa dòng khi cập nhật. Request đến sau phải chờ, rồi đánh giá lại điều kiện trên dữ liệu mới, nên **chỉ 1 request cập nhật được 1 dòng**. Request thắng đọc lại tài khoản (gồm `TokenVersion`) trong cùng transaction rồi mới phát JWT. Request thua nhận 401 và không có token nào.
- **Login:** `TryStoreRefreshTokenAsync` chỉ lưu refresh token nếu tài khoản còn hoạt động và `TokenVersion` chưa đổi.
- **Phối hợp với P1-02:** nếu logout hoặc khóa chen vào ngay sau khi refresh thắng, `TokenVersion` tăng nên token vừa phát cũng bị từ chối.
- Kiểm soát nằm ở server, không dựa vào cơ chế single-flight của frontend.

**File:** `Repositories/IAuthRepository.cs`, `Repositories/AuthRepository.cs`, `Services/AuthService.cs`.

## P2-01 — Số trang cực lớn gây tràn số, trả 500

**Nguyên nhân:** `(page - 1) * pageSize` với `page = 2147483647` tràn số int.

**Giải pháp:**
- Thêm `Constants/PagingLimits.cs` với `MaxPage = 100.000`.
- `[Range(1, MaxPage)]` trên `LeaveRequestQuery.Page`, `EmployeeQueryRequest.Page`, `UserQueryParameters.PageNumber`.
- Mọi `Skip` trong 4 vị trí của Repository dùng `PagingLimits.Skip` (kiểm tra lại + `checked`).
- `GET /api/leave-approvals` kiểm tra số trang trong Service.
- Vượt giới hạn → **400**.

**File:**
- `Constants/PagingLimits.cs`
- `DTOs/Requests/LeaveRequests.cs`, `DTOs/Requests/EmployeeRequests.cs`, `DTOs/Common/UserQueryParameters.cs`
- `Repositories/LeaveRepository.cs`, `Repositories/EmployeeRepository.cs`, `Repositories/UserRepository.cs`
- `Services/LeaveRequestService.cs`

## Test và tài liệu thay đổi

**Test:**
- Thêm `tests/LeaveManage.Tests/RegressionTests.cs` (15 test).
- Trong `LeaveWorkflowTests.cs`, test phạm vi Admin đổi từ "đúng 6 nhân viên" thành "có đủ NV001–NV006", vì regression test tạo thêm nhân viên.

**Tài liệu:**
- Cập nhật `TEST_RESULTS.md`, `IMPLEMENTATION_STATUS.md`, `LEARNING_GUIDE.md`, `DEMO_SCRIPT.md` (đã **bỏ hướng dẫn drop database demo**).
- Thêm file này.
- `.gitignore` thêm `backups/` và `*.bak`.

---

## Kết quả kiểm thử trước / sau

| Hạng mục | Trước (99955c9) | Sau (fce70a4) |
|---|---|---|
| Build Release | 0 warning, 0 lỗi | 0 warning, 0 lỗi |
| 20 test ban đầu | 20/20 | 20/20 |
| 15 regression test mới | **1/15** (14 FAIL đúng các lỗi audit) | **15/15** |
| Tổng xUnit | — | **35/35**, 12 lần chạy liên tiếp đều đạt |
| Sửa nháp ‖ gửi duyệt | TotalDays 2, PendingDays 1 | 30 lượt đua (2 test × 15), số dư luôn khớp; duyệt/hủy sau đó đều 200 |
| JWT Manager cũ sau khi bị khóa | Duyệt được (200) | 401; đơn vẫn Chờ duyệt; mở khóa không làm token sống lại |
| 8 refresh đồng thời cùng token | 2 request thắng | Đúng 1 request thắng (5/5 lượt) |
| `page=2147483647` | 500 | 400 |
| Smoke HTTP | 41/41 (trước audit) | 41/41 (Release) |
| Newman | 28 request / 37 assertion (trước audit) | 28 / 37, 0 lỗi (Release) |
| Giao diện Chrome | 25/25 (trước audit) | 27/27 (thêm kiểm tra đăng xuất/khóa), không lỗi JS |

Chi tiết: [TEST_RESULTS.md](TEST_RESULTS.md).

## Database demo `LeaveManageDb`

1. **Sao lưu trước migration:**
   - File: `backups/LeaveManageDb_before_TokenVersion_20261125.bak` (gitignore).
   - `BACKUP ... WITH CHECKSUM`, `RESTORE VERIFYONLY` báo hợp lệ.
2. Áp migration `AddUserTokenVersion` (chỉ thêm cột).
3. Số dòng và checksum của Departments, LeaveTypes, Positions, Employees, LeaveBalances, LeaveRequests, LeaveApprovals, Users **trước = sau**. Các giá trị này cũng khớp checksum Codex ghi trong audit.
4. 6 tài khoản demo đăng nhập được qua profile https.
5. Không reset, không xóa database nào.

Nếu cần khôi phục (chỉ khi thật sự cần, dừng ứng dụng trước):

```sql
RESTORE DATABASE [LeaveManageDb]
FROM DISK = N'D:\Study\LTW2\Code\LeaveManage\backups\LeaveManageDb_before_TokenVersion_20261125.bak'
WITH REPLACE
```

Sau khi restore phải chạy lại `dotnet ef database update`.

## Vấn đề còn tồn tại

| Mức | Nội dung |
|---|---|
| P3-01 | Mật khẩu dùng HMACSHA512 + salt (cách làm của lab), chưa có work factor như PBKDF2/Argon2. **Không đổi trước buổi bảo vệ**; trả lời đúng là "lựa chọn theo lab, chưa đạt chuẩn production". |
| P3-02 | Chưa có HTTPS redirection/HSTS, chưa giới hạn số lần login; token lưu `sessionStorage`. Chỉ demo localhost. |
| Thiết kế | Mỗi request xác thực thêm 1 truy vấn nhỏ. Mỗi tài khoản giữ 1 refresh token; logout thu hồi mọi phiên của tài khoản (đơn giản, không quản lý đa thiết bị). |
| Chức năng | Như trước: chưa có job Expired, chưa trừ ngày lễ, duyệt 1 cấp, chưa chuyển phép sang năm sau. |
| Kiểm thử | Chưa kiểm thử tải lớn, trình duyệt khác, máy khác. Chưa thực sự restore bản sao lưu. |

## Đề xuất

**GO có điều kiện**, theo đúng điều kiện trong [FINAL_EXAM_READINESS.md](FINAL_EXAM_READINESS.md) mục 2:
- Cả 3 P1 đã sửa; regression đỏ trên bản cũ, xanh trên bản mới, chạy lặp nhiều lần.
- Bộ test cũ vẫn xanh; smoke, Newman, UI (cả 3 vai trò, có đăng xuất/khóa) đều đạt trên bản Release.

Điều kiện còn lại thuộc về nhóm, trước 15:45:
1. Chạy đúng profile https trên máy demo và tự đi một lượt demo theo [DEMO_SCRIPT.md](DEMO_SCRIPT.md).
2. Đọc kỹ các Bước 3b, 3c, 4b trong [LEARNING_GUIDE.md](LEARNING_GUIDE.md) để tự giải thích được 3 bản sửa.
3. Khi bảo vệ, nêu đúng các giới hạn P3; không tuyên bố hệ thống đạt chuẩn production.
