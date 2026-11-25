# Kịch bản demo LeaveManage (8–10 phút)

Phiên bản demo: commit `fce70a4` (đã sửa các lỗi theo audit; xem [FINAL_FIX_REPORT.md](FINAL_FIX_REPORT.md)).

## A. Checklist trước giờ demo (làm trước 15 phút)

1. Mở PowerShell tại `D:\Study\LTW2\Code\LeaveManage`. Chạy `git log --oneline -1` và `git status`: phải đúng commit đóng băng và không có thay đổi lạ.
2. Build: `dotnet build LeaveManage.sln -c Release`. Kết quả phải là 0 lỗi.
3. Chạy test: `dotnet test LeaveManage.sln -c Release`. Kết quả mong đợi **35/35 Passed**. Test chỉ dùng database `LeaveManage_Test`, không đụng dữ liệu demo.
4. Kiểm tra bí mật **riêng trên máy, không chiếu lên màn hình**: `Jwt:Key` và `Seed:DemoPassword` phải có trong user-secrets.
5. Chạy: `dotnet run --launch-profile https`. Đợi dòng `Database LeaveManageDb sẵn sàng`.
   - `LeaveManageDb` đã được áp migration `AddUserTokenVersion`. Migration chỉ thêm cột, có bản sao lưu trong `backups/`.
6. Mở sẵn 2 tab:
   - https://localhost:7103/ (giao diện)
   - https://localhost:7103/swagger
7. Mở file `demo-accounts.local.txt` để lấy mật khẩu demo. **Không chiếu file này lên màn hình.**
8. **Không xóa hay reset `LeaveManageDb`.**
   - Nếu dữ liệu demo bị rối, cứ dùng tiếp (tạo đơn ở ngày khác).
   - Nếu buộc phải khôi phục, dùng bản sao lưu trong `backups/` (xem [FINAL_FIX_REPORT.md](FINAL_FIX_REPORT.md)), **không** dùng lệnh drop.

Lưu ý:
- **Ngày nghỉ khi demo:** chọn một **ngày làm việc cách hôm nay ít nhất 4 ngày** (phép năm phải báo trước 3 ngày).
- **Token cũ:** đăng xuất một tài khoản trên giao diện sẽ thu hồi mọi token của tài khoản đó, kể cả token đang dán trong Swagger. Nếu Swagger báo 401, đăng nhập lại để lấy token mới.

## B. Kịch bản

| # | Ai | Thao tác | Điều cần nói |
|---|---|---|---|
| 1 | — | Mở Swagger, chỉ các nhóm API | Web API 3 lớp; 58 endpoint; Swagger chỉ bật ở Development |
| 2 | — | Gọi `GET /api/leave-requests` khi chưa Authorize | **401**: API được bảo vệ bằng `[Authorize]` |
| 3 | nv003 | Swagger: `POST /api/Users/login` → chép token → Authorize | JWT + Refresh Token; token có claim `EmployeeId` và `TokenVersion` |
| 4 | nv003 | Gọi `GET /api/Users` | **403**: User gọi API của Admin |
| 5 | nv003 | Giao diện: đăng nhập nv003 → Tổng quan | Số dư phép năm 12/12; menu chỉ có chức năng của nhân viên |
| 6 | nv003 | Đơn nghỉ của tôi → Tạo đơn: Phép năm, 2 ngày làm việc | Lưu **Nháp**; hệ thống đếm ngày Thứ 2 – Thứ 6 |
| 7 | nv003 | Thử tạo đơn với ngày kết thúc trước ngày bắt đầu | **400** hiển thị lỗi tiếng Việt |
| 8 | nv003 | Bấm "Gửi duyệt" | Trạng thái **Chờ duyệt**; Tổng quan: "Đang chờ duyệt 2", còn lại 10 |
| 9 | nv003 | Tạo thêm đơn trùng ngày rồi gửi duyệt | **409 OVERLAPPING_REQUEST** |
| 10 | manager | Đăng xuất → đăng nhập manager → Duyệt đơn | Manager chỉ thấy đơn của cấp dưới trực tiếp. Đăng xuất thu hồi token của nv003. |
| 11 | manager | Bấm Duyệt (ghi chú "Đồng ý") | PendingDays → UsedDays; ghi `LeaveApproval` |
| 12 | manager | Danh sách đơn nghỉ → Xem chi tiết | Lịch sử duyệt: người duyệt, thời điểm, ghi chú |
| 13 | admin | Swagger: login admin → `POST /api/leave-requests/{id}/approve` cùng đơn | **409 ALREADY_PROCESSED**: không xử lý 2 lần |
| 14 | — | Mở test `Concurrency_TwoApproversAtOnce...` và `P1_01_EditDaysWhileSubmitting...` | Duyệt song song: 1 lần 200, 1 lần 409. Sửa nháp đua với gửi duyệt: số dư luôn khớp số ngày của đơn (transaction + `UPDLOCK`) |
| 15 | nv003 | Hủy đơn đã duyệt (chưa tới ngày nghỉ) | Trả lại số dư (UsedDays giảm) |
| 16 | admin | Danh mục → thêm phòng ban mã "IT" | **409** trùng mã |
| 17 | admin | Tài khoản → Cấp tài khoản, gắn nhân viên đã có tài khoản | **409 EMPLOYEE_ALREADY_LINKED** (User–Employee 1-0..1) |
| 18 | — | Thu nhỏ trình duyệt (mobile) | Giao diện responsive, menu offcanvas |

**Phần tùy chọn (nếu giảng viên hỏi về khóa tài khoản):**
1. Mở thêm một cửa sổ đăng nhập nv004.
2. Admin vào trang Tài khoản → Khóa nv004.
3. Bên cửa sổ nv004, bấm sang trang khác → bị đưa về trang đăng nhập (JWT cũ trả 401).
4. Mở khóa lại nv004 ngay sau đó.

## C. Nếu có sự cố

| Sự cố | Cách xử lý |
|---|---|
| Lỗi "Thiếu Jwt:Key" khi chạy | `dotnet user-secrets set "Jwt:Key" "<chuỗi ≥ 32 ký tự>"` |
| Không đăng nhập được | Kiểm tra `demo-accounts.local.txt` và tài khoản có đang bị khóa không (Admin → Tài khoản). |
| Swagger trả 401 với token vừa dùng | Tài khoản đã đăng xuất, bị khóa hoặc đổi quyền nên token cũ bị thu hồi. Đăng nhập lại. |
| Lỗi chứng chỉ HTTPS | `dotnet dev-certs https --trust` hoặc dùng `--launch-profile http` (http://localhost:5103) |
| LocalDB không chạy | `sqllocaldb start MSSQLLocalDB` |
| Đơn bị 400 NOTICE_REQUIRED | Chọn ngày bắt đầu xa hơn (phép năm phải báo trước 3 ngày) |
