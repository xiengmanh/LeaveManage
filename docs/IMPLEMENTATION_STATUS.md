# Trạng thái triển khai — LeaveManage

Cập nhật ngày 25/11/2026. Mã nguồn được đóng băng bằng commit Git local; nhóm chưa push lên remote và chưa deploy.

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
| Refresh token | Lưu bản băm SHA-256, xoay vòng, thu hồi khi logout hoặc khi khóa tài khoản. |
| Đăng ký công khai | `register` chỉ Admin dùng được, có chọn role và gắn nhân viên. |
| Middleware coi mọi `InvalidOperationException` là 400 | Bỏ ánh xạ đó. Thêm ánh xạ 409 cho lỗi unique, xung đột đồng thời, khóa ngoại. |
| `PageNumber` không có giới hạn dưới | Ép tối thiểu 1. |
| Swagger bật cả ở Production | Chỉ bật ở Development. |
| Endpoint minh họa ẩn danh (System, Validation, Errors) | Chuyển sang chỉ Admin. |
| Admin có thể tự khóa / tự xóa / tự hạ quyền | API trả 400 `CANNOT_CHANGE_SELF`. |

## 3. Đã hoàn thành

- **Xác thực:** login, refresh (xoay vòng), logout, `me`. Khóa tài khoản thì không đăng nhập và không refresh được.
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
- **Đồng thời:** transaction + `UPDLOCK` theo nhân viên; unique `LeaveApprovals.LeaveRequestId`.
- **Giao diện web** tiếng Việt, responsive, dùng API thật cho cả 3 vai trò; không có nút giả.
- **Kiểm thử:**
  - 20 integration test (xUnit);
  - Postman collection 28 request;
  - kiểm thử giao diện tự động trên Chrome;
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
| HTTPS ở môi trường thật, deploy | Không thực hiện (yêu cầu: không deploy). |
| Báo cáo Word / PDF / PowerPoint | Không tạo theo yêu cầu. Chờ mẫu báo cáo chính thức. |

## 5. Quyết định thiết kế cần nhớ khi bảo vệ

- Mọi loại nghỉ đều cần duyệt (`RequiresApproval = true`); không có duyệt tự động.
- Ngoài phạm vi xem thì trả **404** để không lộ dữ liệu. Trong phạm vi nhưng không đủ quyền thao tác thì trả **403**. Ví dụ: Manager bấm "gửi duyệt" đơn của cấp dưới → 403.
- Phép ốm (SICK) có `AllowsDeduction = true` theo dữ liệu mẫu, nên cũng trừ số dư 30 ngày/năm.
- Nghỉ không lương (`DefaultDaysPerYear = null`) có `PaidDays = 0`. Các loại khác `PaidDays = TotalDays`.
