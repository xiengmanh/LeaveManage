# Kịch bản demo LeaveManage (8–10 phút)

## A. Checklist trước giờ demo (làm trước 15 phút)

1. Mở PowerShell tại `D:\Study\LTW2\Code\LeaveManage`.
2. Build: `dotnet build -c Release`. Kết quả phải là 0 lỗi.
3. Chạy test: `dotnet test LeaveManage.sln`. Kết quả mong đợi 20/20 Passed. Test dùng database `LeaveManage_Test`, không đụng dữ liệu demo.
4. Kiểm tra bí mật: `dotnet user-secrets list`. Phải có `Jwt:Key` và `Seed:DemoPassword`.
5. Chạy: `dotnet run --launch-profile https`. Đợi dòng `Database LeaveManageDb sẵn sàng`.
6. Mở sẵn 2 tab:
   - https://localhost:7103/ (giao diện)
   - https://localhost:7103/swagger
7. Mở file `demo-accounts.local.txt` để lấy mật khẩu demo. **Không chiếu file này lên màn hình.**
8. Nếu muốn dữ liệu demo sạch lại từ đầu (chỉ database demo, **không** đụng database khác):
   `dotnet ef database drop --force` rồi chạy lại ứng dụng. Ứng dụng sẽ tự migrate và seed.

Ngày nghỉ khi demo: chọn một **ngày làm việc cách hôm nay ít nhất 4 ngày**, vì phép năm phải báo trước 3 ngày.

## B. Kịch bản

| # | Ai | Thao tác | Điều cần nói |
|---|---|---|---|
| 1 | — | Mở Swagger, chỉ các nhóm API | Web API 3 lớp; 58 endpoint; Swagger chỉ bật ở Development |
| 2 | — | Gọi `GET /api/leave-requests` khi chưa Authorize | **401**: API được bảo vệ bằng `[Authorize]` |
| 3 | nv003 | Swagger: `POST /api/Users/login` → chép token → Authorize | JWT + Refresh Token; token có claim `EmployeeId` |
| 4 | nv003 | Gọi `GET /api/Users` | **403**: User gọi API của Admin |
| 5 | nv003 | Giao diện: đăng nhập nv003 → Tổng quan | Số dư phép năm 12/12; menu chỉ có chức năng của nhân viên |
| 6 | nv003 | Đơn nghỉ của tôi → Tạo đơn: Phép năm, 2 ngày làm việc | Lưu **Nháp**; hệ thống đếm ngày Thứ 2 – Thứ 6 |
| 7 | nv003 | Thử tạo đơn với ngày kết thúc trước ngày bắt đầu | **400** hiển thị lỗi tiếng Việt |
| 8 | nv003 | Bấm "Gửi duyệt" | Trạng thái **Chờ duyệt**; Tổng quan: "Đang chờ duyệt 2", còn lại 10 |
| 9 | nv003 | Tạo thêm đơn trùng ngày rồi gửi duyệt | **409 OVERLAPPING_REQUEST** |
| 10 | manager | Đăng xuất → đăng nhập manager → Duyệt đơn | Manager chỉ thấy đơn của cấp dưới trực tiếp |
| 11 | manager | Bấm Duyệt (ghi chú "Đồng ý") | PendingDays → UsedDays; ghi `LeaveApproval` |
| 12 | manager | Danh sách đơn nghỉ → Xem chi tiết | Lịch sử duyệt: người duyệt, thời điểm, ghi chú |
| 13 | admin | Swagger: login admin → `POST /api/leave-requests/{id}/approve` cùng đơn | **409 ALREADY_PROCESSED**: không xử lý 2 lần |
| 14 | — | Mở test `Concurrency_TwoApproversAtOnce...` | 2 lệnh duyệt song song: 1 lần 200, 1 lần 409, trừ số dư 1 lần (transaction + `UPDLOCK`) |
| 15 | nv003 | Hủy đơn đã duyệt (chưa tới ngày nghỉ) | Trả lại số dư (UsedDays giảm) |
| 16 | admin | Danh mục → thêm phòng ban mã "IT" | **409** trùng mã |
| 17 | admin | Tài khoản → Cấp tài khoản, gắn nhân viên đã có tài khoản | **409 EMPLOYEE_ALREADY_LINKED** (User–Employee 1-0..1) |
| 18 | — | Thu nhỏ trình duyệt (mobile) | Giao diện responsive, menu offcanvas |

## C. Nếu có sự cố

| Sự cố | Cách xử lý |
|---|---|
| Lỗi "Thiếu Jwt:Key" khi chạy | `dotnet user-secrets set "Jwt:Key" "<chuỗi ≥ 32 ký tự>"` |
| Không đăng nhập được | Kiểm tra `demo-accounts.local.txt`. Tài khoản chỉ được tạo khi DB mới và `Seed:DemoPassword` có giá trị. |
| Lỗi chứng chỉ HTTPS | `dotnet dev-certs https --trust` hoặc dùng `--launch-profile http` (http://localhost:5103) |
| LocalDB không chạy | `sqllocaldb start MSSQLLocalDB` |
| Đơn bị 400 NOTICE_REQUIRED | Chọn ngày bắt đầu xa hơn (phép năm phải báo trước 3 ngày) |
