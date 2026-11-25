# Trạng thái triển khai — LeaveManage

Cập nhật ngày 25/11/2026, sau khi sửa lỗi theo audit độc lập ([CODEX_FINAL_AUDIT.md](CODEX_FINAL_AUDIT.md), chi tiết sửa ở [FINAL_FIX_REPORT.md](FINAL_FIX_REPORT.md)). Mã nguồn đóng băng bằng commit Git local `fce70a4`; nhóm chưa push lên remote và chưa deploy.

## 1. Nguồn gốc

- **Phát triển tiếp từ LTW22**, không viết lại từ đầu.
- Thư mục `LTW22` gốc **không bị sửa**.
- Các bước thực hiện:
  1. Sao lưu LTW22 thành `LTW22_backup_20261125`.
  2. Sao chép LTW22 sang `LeaveManage`. Không đổi tên tại chỗ được vì thư mục gốc đang được công cụ làm việc mở.
  3. Đổi `LTW22.csproj` thành `LeaveManage.csproj`, đổi namespace, tiêu đề Swagger và cấu hình.
- **Database mới:**
  - `LeaveManageDb` cho dev/demo;
  - `LeaveManage_Test` cho kiểm thử.
- **Không dùng, không xóa:** `LeaveManagementDB` và `UniManageDb`.
- Tham khảo: `LeaveManagementApi` (không ghi đè) và `UniManage`. Phần giao diện chỉ **sao chép** component từ UniManage; thư mục UniManage không bị sửa.

## 2. Các vấn đề của LTW22 đã xử lý

| Vấn đề | Cách xử lý |
|---|---|
| Hai cơ chế mật khẩu (`Employee.PasswordHash` dạng SHA-256 + salt cố định "LTW22_Salt", và `User`) | Bỏ mật khẩu trên Employee. Chỉ còn `User` với HMACSHA512 + salt ngẫu nhiên (`PasswordService`). |
| User và Employee không liên kết | Thêm `User.EmployeeId` (1-0..1, unique có lọc). JWT mang claim `EmployeeId`. |
| Quan hệ Manager | Self-reference `ManagerId`, `NoAction`. Chặn tự quản lý mình và chặn vòng lặp quản lý (`IsInReportingChainAsync`). |
| Khóa chính LeaveBalance | Dùng `Id` làm khóa chính, unique (EmployeeId, LeaveTypeId, Year). |
| JWT Key trong `appsettings.json` | Chuyển sang user-secrets. Thiếu key hoặc key < 32 byte thì không khởi động. |
| CORS AllowAll | Chỉ cho origin trong `Cors:AllowedOrigins`. |
| API đọc nhân viên không giới hạn theo quyền | Áp phạm vi Admin / Manager / User, ngoài phạm vi trả 404. |
| Refresh token | Lưu bản băm SHA-256. Xoay vòng **nguyên tử**: `UPDATE` có điều kiện, nhiều request dùng cùng token thì chỉ 1 request thắng (sửa P1-03). Thu hồi khi logout hoặc khi khóa tài khoản. |
| Access JWT cũ vẫn dùng được sau khi khóa / logout (audit P1-02) | Thêm `User.TokenVersion` và claim `TokenVersion`. Mỗi request kiểm tra: tài khoản còn tồn tại, còn hoạt động, đúng `TokenVersion`, đúng Role và EmployeeId. Logout, khóa, đổi role, đổi nhân viên liên kết hoặc đổi mật khẩu đều tăng `TokenVersion`. |
| Sửa nháp đua với gửi duyệt làm lệch số dư (audit P1-01) | `UpdateDraftAsync` dùng cùng transaction và khóa `UPDLOCK` nhân viên như Submit/Approve/Cancel; sau khi khóa mới đọc lại đơn, kiểm tra chủ đơn và trạng thái Nháp. |
| Đăng ký công khai | `register` chỉ Admin dùng được, có chọn role và gắn nhân viên. |
| Middleware coi mọi `InvalidOperationException` là 400 | Bỏ ánh xạ đó. Thêm ánh xạ 409 cho lỗi unique, xung đột đồng thời, khóa ngoại. |
| `PageNumber` không có giới hạn dưới | Ép tối thiểu 1. |
| Số trang cực lớn gây tràn số, trả 500 (audit P2-01) | Giới hạn số trang 1–100.000 (`[Range]` trên DTO, kiểm tra lại trong `PagingLimits.Skip`, phép nhân dùng `checked`). Vượt giới hạn trả 400. |
| Swagger bật cả ở Production | Chỉ bật ở Development. |
| Endpoint minh họa ẩn danh (System, Validation, Errors) | Chuyển sang chỉ Admin. |
| Admin có thể tự khóa / tự xóa / tự hạ quyền | API trả 400 `CANNOT_CHANGE_SELF`. |

## 3. Đã hoàn thành

- **Xác thực:**
  - login, refresh (xoay vòng nguyên tử), logout, `me`;
  - khóa tài khoản: không đăng nhập, không refresh được, và **JWT đã cấp trước đó bị từ chối ngay**;
  - logout thu hồi cả refresh token lẫn access JWT, kể cả khi người dùng đăng nhập lại sau đó.
- **Tài khoản (Admin):** danh sách (phân trang, tìm kiếm, lọc role), tạo, sửa (role, gắn nhân viên, khóa/mở, đặt lại mật khẩu), xóa.
- **Nhân viên:**
  - danh sách phân trang/tìm kiếm/lọc theo phạm vi; chi tiết theo phạm vi;
  - Admin thêm/sửa/cho nghỉ việc (soft delete).
- **Danh mục phòng ban, chức vụ, loại nghỉ:** CRUD (Admin); "xóa" nghĩa là ngừng sử dụng.
- **Đơn nghỉ:** tạo nháp, sửa nháp, gửi duyệt, duyệt, từ chối (bắt buộc lý do), hủy. Danh sách có phân trang/lọc/tìm kiếm; có danh sách chờ duyệt và chi tiết kèm lịch sử duyệt.
- **Quy tắc đơn nghỉ:** báo trước, trùng ngày, số dư, giới hạn ngày/năm, đếm ngày làm việc, không vắt năm.
- **Số dư phép:** xem theo phạm vi; Admin cấp và điều chỉnh (không được thấp hơn số đã dùng + đang chờ).
- **Lịch sử duyệt:** theo phạm vi.
- **Dashboard:** theo phạm vi.
- **Đồng thời:** mọi thao tác thay đổi đơn đã có hoặc số dư (sửa nháp, gửi duyệt, duyệt, từ chối, hủy, Admin điều chỉnh số dư; tạo đơn nháp mới không động đến số dư nên không cần khóa) chạy trong transaction + `UPDLOCK` theo nhân viên; unique `LeaveApprovals.LeaveRequestId`.
- **Giao diện web** tiếng Việt, responsive, dùng API thật cho cả 3 vai trò; không có nút giả.
- **Kiểm thử:**
  - 35 test xUnit (20 test ban đầu + 15 regression test cho các lỗi audit);
  - Postman collection 28 request;
  - kiểm thử giao diện tự động trên Chrome (27 kiểm tra);
  - smoke test HTTP 41 bước.

## 4. Chưa làm / giới hạn (ghi rõ, không giả lập)

| Hạng mục | Tình trạng |
|---|---|
| Trạng thái `Expired` | Có trong enum nhưng **không có tác vụ tự động** chuyển đơn quá hạn sang Expired. API không bao giờ tự đặt giá trị này. |
| Ngày lễ | Chưa trừ ngày lễ khi đếm ngày nghỉ (chỉ bỏ T7/CN). |
| Duyệt nhiều cấp | Chỉ 1 cấp (`ApprovalLevel = 1`). `LeaveApproval` unique theo đơn. |
| Chuyển phép sang năm sau | Cột `CarriedForwardDays`/`MaxCarryForwardDays` có sẵn từ LTW22 nhưng chưa có nghiệp vụ. |
| Đơn của Admin không có quản lý | Admin NV001 không có quản lý trực tiếp và không được tự duyệt, nên đơn của NV001 chỉ được duyệt khi có Admin khác. |
| Tải lại trang đúng lúc đang refresh token | Refresh token đã bị xoay vòng ở server nhưng trình duyệt chưa kịp lưu cặp mới, nên người dùng phải đăng nhập lại. |
| Phân trang số dư / danh mục | Danh sách ngắn, không phân trang (số dư giới hạn 500 dòng). |
| Upload file (`/api/Files`) | Giữ nguyên chức năng lab của LTW22, chưa gắn vào nghiệp vụ nghỉ phép (ví dụ đính kèm giấy ốm). Giao diện không có nút upload. |
| Lưu mật khẩu (audit P3-01) | HMACSHA512 + salt ngẫu nhiên theo cách làm của lab, **chưa phải** thuật toán chuyên dụng có work factor (PBKDF2/`PasswordHasher`, Argon2). Phù hợp đồ án, chưa đạt chuẩn vận hành thật. Chưa đổi trước buổi bảo vệ. |
| Hardening triển khai thật (audit P3-02) | Chưa có HTTPS redirection/HSTS, chưa giới hạn số lần đăng nhập. Token lưu trong `sessionStorage`. Chỉ demo trên localhost. |
| Kiểm tra JWT mỗi request | Mỗi request có xác thực tốn thêm 1 truy vấn nhỏ (theo khóa chính `Users`). Chấp nhận được ở quy mô đồ án. |
| Nhiều thiết bị | Mỗi tài khoản chỉ giữ 1 refresh token. Đăng nhập ở nơi khác thay refresh token cũ; logout thu hồi mọi phiên của tài khoản. |
| HTTPS ở môi trường thật, deploy | Không thực hiện (yêu cầu: không deploy). |
| Báo cáo Word / PDF / PowerPoint | Không tạo theo yêu cầu. Chờ mẫu báo cáo chính thức. |

## 5. Quyết định thiết kế cần nhớ khi bảo vệ

- Mọi loại nghỉ đều cần duyệt (`RequiresApproval = true`); không có duyệt tự động.
- Ngoài phạm vi xem thì trả **404** để không lộ dữ liệu. Trong phạm vi nhưng không đủ quyền thao tác thì trả **403**. Ví dụ: Manager bấm "gửi duyệt" đơn của cấp dưới → 403.
- Phép ốm (SICK) có `AllowsDeduction = true` theo dữ liệu mẫu, nên cũng trừ số dư 30 ngày/năm.
- Nghỉ không lương (`DefaultDaysPerYear = null`) có `PaidDays = 0`. Các loại khác `PaidDays = TotalDays`.
