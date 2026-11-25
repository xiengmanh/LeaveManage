# LeaveManage - sẵn sàng bảo vệ lúc 15:45 ngày 25/11/2026

Commit được audit: **99955c9**. Đúng dự án kế thừa LTW22, đúng đề tài back-end quản lý ngày nghỉ nhân sự, nhóm 12.

## 1. Quyết định hiện tại

**NO-GO cho bản đóng băng hiện tại về bảo mật/tính nhất quán cốt lõi.** Không phải vì thiếu tính năng hoặc không build được. Release build 0 lỗi/cảnh báo, xUnit 20/20 đạt, nhưng kiểm thử bổ sung đã tìm được **3 P1**:

1. Sửa Draft đồng thời Submit -> đơn 2 ngày nhưng PendingDays chỉ 1; approve/cancel sau đó cùng 409.
2. Khóa Manager chặn login/refresh nhưng JWT cũ vẫn approve thành công.
3. Một refresh token dùng đồng thời 5 lần có thể trả nhiều cặp token, rotation không nguyên tử.

Ngoài ra: 1 P2 (page cực lớn gây 500), 2 P3 (HMAC password và hardening production). Không có P0 được phát hiện trong phạm vi audit.

**Việc cần làm ngay chỉ là sửa ba P1 và kiểm thử lại**, không làm lại app, không đổi kiến trúc, không thêm tính năng. Chi tiết code/tái hiện nằm ở [CODEX_FINAL_AUDIT.md](CODEX_FINAL_AUDIT.md), mục 5.

## 2. Điều kiện chuyển GO có điều kiện

- [ ] UpdateDraft dùng cùng giao thức khóa/transaction với Submit; kiểm tra lại Draft sau khóa. Không còn TotalDays lệch PendingDays khi chạy cạnh tranh nhiều lượt.
- [ ] Token lấy trước khi khóa account không còn GET/approve được; xác định và kiểm thử rõ access-token behavior sau logout/đổi quyền.
- [ ] Cùng refresh token, 5-10 request đồng thời chỉ một request thắng; token mới thắng dùng được, token cũ không reuse; phối hợp đúng với logout/khóa.
- [ ] Chạy Release build và toàn test; thêm regression cho ba P1 chứ không chỉ trình lại 20 test xanh cũ.
- [ ] Tự kiểm tra một lượt frontend đúng profile và ba vai trò; ghi rõ phần chưa chạy.

Không chuyển thành GO chỉ vì không chạm vào các tình huống lỗi khi demo. Nếu không sửa/kiểm chứng được trước giờ chốt, phải công bố đúng hạn chế; không khẳng định “khóa tài khoản là ngăn mọi truy cập” hoặc “mọi thao tác đều concurrency-safe”.

## 3. Checklist chạy demo trên máy hiện tại

### Môi trường

- [ ] Đứng đúng thư mục `D:\Study\LTW2\Code\LeaveManage`.
- [ ] Kiểm tra version source đã sửa/chốt, `git status`, connection string **LeaveManageDb** cho demo; **LeaveManage_Test** chỉ dùng test.
- [ ] Chạy `dotnet build LeaveManage.sln -c Release` và `dotnet test LeaveManage.sln -c Release`.
- [ ] Nhớ xUnit tự tạo lại DB test. Database test đang có fixture audit lỗi; **không dùng DB test làm demo, không drop LeaveManageDb để dọn fixture**.
- [ ] Bí mật đã cấu hình ở User Secrets; kiểm tra riêng, không chiếu key/password/refresh token hoặc `user-secrets list` lên màn hình.
- [ ] Chạy `dotnet run --project LeaveManage.csproj --launch-profile https`.
- [ ] Mở **https://localhost:7103/** và **https://localhost:7103/swagger**. Profile HTTP là 5103; cổng audit 7643 không phải profile demo.
- [ ] Audit đã chạy Release thành công trên cổng test; tại lúc probe cổng demo 7103 chưa có server. Cần thực sự khởi động profile, không chỉ mở URL.
- [ ] Xác nhận LocalDB, credential demo và HTTPS tại máy. Không update SDK/package hoặc reset database ngay trước buổi bảo vệ.

### Luồng demo 8-10 phút

1. Giới thiệu đúng đề tài: nhân sự, loại nghỉ, đơn, balance, approval; backend phát triển tiếp LTW22. Giao diện là client gọi API thật.
2. Swagger chưa Authorize -> GET đơn nhận 401. Login User -> API tài khoản Admin nhận 403.
3. User nv003 xem hồ sơ/số dư của mình, tạo Draft vào ngày làm việc cách hôm nay ít nhất 4 ngày. Draft chưa trừ phép.
4. Submit -> PendingDays tăng, RemainingDays giảm. Tạo đơn trùng ngày/thiếu số dư -> 409.
5. Manager vào đơn chờ trực thuộc, duyệt -> Pending giảm/Used tăng, Remaining không bị trừ lần hai; xem một bản ghi approval.
6. User hủy đơn đã duyệt trước ngày bắt đầu -> Used giảm, trả phép. Minh họa reject cần lý do nếu còn thời gian.
7. Chỉ code: CallerContext/DataScope, ownership/self-approval, LeaveRepository transaction + UPDLOCK, PasswordService, JWT.
8. Nêu đúng test đã chạy và hạn chế: không ngày lễ, một cấp duyệt, chưa carry-forward/job Expired; không nhận giới hạn thành tính năng đã làm.

Trước lúc lên bảo vệ, tự thử ngắn:

- [ ] Tạo/sửa/gửi/duyệt/hủy trên UI; số dư trước/sau khớp.
- [ ] Admin/Manager tự duyệt -> 403; người khác xem/gửi/hủy -> 404/403 đúng quy tắc.
- [ ] Khóa account rồi thử JWT cũ sau bản sửa; logout/refresh sau bản sửa đúng contract.
- [ ] Modal lỗi, Tab/Enter/Esc, menu mobile, không có lỗi JavaScript ngoài dự kiến.
- [ ] Đọc response 409 và TraceId thay vì gọi mọi lỗi là “lỗi server”.

Browser UI/25 case cũ của Claude **chưa được audit chạy lại** vì browser host không khả dụng; 20 JS syntax check và asset 200 không thay thế các ô trên.

## 4. Sáu file cần tự giải thích được

| File | Điều phải nắm |
|---|---|
| `Program.cs` | DI scoped/singleton; JwtBearer/policy; auth trước authorization; không có account revalidation ở bản cũ |
| `Services/PasswordService.cs` | HMACSHA512 + salt/key ngẫu nhiên; FixedTimeEquals; vì sao không phải password KDF có work factor |
| `Services/AuthService.cs` | Access 60 phút, refresh 64 byte ngẫu nhiên/hash SHA256/expiry; rotation và logout; lỗi read-then-write cạnh tranh |
| `Services/CallerContext.cs` | EmployeeId/role từ JWT; DataScope Admin/Manager/User; scope không thay thế trạng thái account hiện tại |
| `Services/LeaveRequestService.cs` | Rule owner/direct manager/self-approval, transition, Pending/Used; nhánh UpdateDraft cần khóa giống các mutation khác |
| `Repositories/LeaveRepository.cs` | ReadCommitted + UPDLOCK theo Employee; mọi writer phải tuân thủ; Save/commit/rollback; query scope/filter/paging |

Không nhầm `HttpContext` với `DbContext`. “Không được dùng context” ở đây là **không truy cập AppDbContext trực tiếp trong Controller/Service**; Repository/Data vẫn cần EF Core DbContext.

## 5. Mười câu phản biện ngắn để ôn

**1. Có đúng đề tài và kế thừa LTW22 không?** Có. Models/DTO/Repo/Auth và lab được kế thừa, thêm workflow nghỉ/số dư/approval, liên kết User-Employee và scope. Không phải hệ thống sinh viên.

**2. Vì sao tách ba tầng?** Controller lo HTTP, Service nghiệp vụ/quyền, Repository EF/query/transaction. Interface và DI giúp thay thế/test; không cần tách thành nhiều project mới đúng kiến trúc.

**3. Mật khẩu lưu ra sao?** HMACSHA512 với key ngẫu nhiên riêng đóng vai trò salt; lưu hash/salt, verify thời gian cố định, dùng chung khi seed/tạo/login. Đây là cơ chế lab; không nói tương đương PBKDF2/Argon2 cho production.

**4. JWT là mã hóa hay ký?** Ký HS256, payload đọc được. Kiểm tra signature/issuer/audience/expiry không tự kiểm tra account còn active; bản sửa phải xử lý điều đó.

**5. Logout khác hết hạn token thế nào?** Bản 99955c9 logout chỉ xóa refresh hash; access JWT còn hạn vẫn dùng được. Chỉ nói đã thu hồi cả access nếu bản sửa và regression đã chứng minh.

**6. Refresh rotation có an toàn chưa?** Ngẫu nhiên và hash storage đúng; tuần tự token cũ 401, nhưng bản cũ cạnh tranh có nhiều winner. Cần tiêu thụ hash cũ nguyên tử, không chỉ dựa frontend single-flight.

**7. Ai duyệt được?** Admin hoặc Manager trực tiếp, không tự duyệt kể cả Admin. Role check ở Controller/policy, rule ownership/direct manager/self-approval ở Service.

**8. Tính phép thế nào?** T2-T6, tính cả ngày đầu/cuối, chưa trừ lễ. Remaining = Initial - Used - Pending. Submit giữ Pending; approve chuyển Pending sang Used; reject/cancel giải phóng đúng một lần theo trạng thái.

**9. Có transaction rồi sao còn lỗi số dư?** UPDLOCK chỉ có tác dụng nếu mọi writer cùng lấy khóa. UpdateDraft bản cũ không khóa nên có thể ghi sau khi Submit đã tính số ngày khác; phải kiểm tra lại trạng thái sau khóa.

**10. Test xanh có chứng minh hết lỗi không?** Không. 20 test phủ nhiều happy/invalid/scope/race chuẩn; audit thêm ca edit-submit, khóa với JWT cũ, refresh đồng thời mới tìm bug. Phải bổ sung test đúng lỗi, không chỉ tăng số lượng test.

## 6. Không làm trước giờ bảo vệ

Không mở rộng thành HRM, duyệt nhiều cấp/ngày lễ/carry-forward, thay toàn bộ password scheme, chuyển framework, deploy hay tạo thêm tài liệu Word/PDF/PowerPoint. Giữ thời gian còn lại cho ba bản sửa P1, một lượt regression/demo và tự hiểu code.

Audit không sửa source và không thay đổi LeaveManageDb. Sau audit server cổng test sẽ được dừng; source và database demo không bị reset. Kết quả chi tiết, cách tái hiện và bằng chứng đều nằm trong hai Markdown này.
