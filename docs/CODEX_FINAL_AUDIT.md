# LeaveManage - Final Independent Audit

Ngày audit: **25/11/2026**, buổi trưa, Asia/Saigon. Mốc bảo vệ người dùng cung cấp: **15:45 cùng ngày**.

Dự án: `D:\Study\LTW2\Code\LeaveManage`. Commit kiểm tra: **99955c9042a9a1a7327e78d3d18817b636e98da7**. Git worktree sạch lúc bắt đầu.

## 1. Kết luận ngắn

**NO-GO cho việc xác nhận bản 99955c9 an toàn và nhất quán ở các chức năng cốt lõi.** Ứng dụng khởi động được, các luồng demo thông thường chạy đúng; không cần làm lại dự án. Tuy nhiên audit độc lập tái hiện ba lỗi P1, trong đó một lỗi làm đơn nghỉ và số dư không khớp, sau đó không duyệt/hủy được qua API.

| Mức | Số lượng | Nội dung |
|---|---:|---|
| P0 | 0 | Chưa phát hiện trong phạm vi kiểm tra; không phải bảo đảm tuyệt đối không tồn tại |
| P1 | 3 | Race sửa nháp/gửi duyệt gây lệch phép; JWT cũ sau khóa vẫn duyệt được; refresh rotation không nguyên tử |
| P2 | 1 | Số trang rất lớn làm tràn phép tính Skip, trả 500 |
| P3 | 2 | Giới hạn HMAC cho lưu mật khẩu thực tế; hardening khi triển khai ngoài môi trường demo |

**Chỉ ưu tiên sửa ba P1**, thêm regression và chạy lại test. P2 là sửa nhỏ nếu còn thời gian; P3 không phải yêu cầu đổi công nghệ trước giờ bảo vệ. Không yêu cầu viết báo cáo Word/PDF/PowerPoint, thêm tính năng hay thay kiến trúc.

Sau khi ba P1 được sửa và kiểm chứng, có thể đánh giá lại **GO có điều kiện** với một lượt chạy UI/demo ngắn. Audit này không tự sửa source.

## 2. Phạm vi và bảo toàn dữ liệu

- Chỉ audit LeaveManage; không kiểm tra lại UniManage.
- Đọc Controller/Service/Repository/Data/Models/DTO, test, cấu hình, code frontend liên quan, tài liệu triển khai và nguồn LTW22 tương ứng.
- Chạy Release và xUnit trên máy hiện tại. Fixture đã được đọc trước: chỉ xóa/tạo lại **LeaveManage_Test**, không reset database demo.
- HTTP độc lập chạy bản Release tại **https://localhost:7643**, connection string chỉ tới **LeaveManage_Test**, dùng key và credential **chỉ dành cho test**. Không lưu giá trị token/password/key vào báo cáo.
- Không chạy lại profile Development trỏ LeaveManageDb vì startup có migrate/seed. Cổng demo theo cấu hình là **7103 HTTPS / 5103 HTTP**; cổng 7643 chỉ là audit.
- Không sửa source, không thêm project/script/file bằng chứng vào repository, không push/deploy. Chỉ tạo hai Markdown audit theo yêu cầu. File build/cache do công cụ sinh là sản phẩm kiểm thử thông thường.

### Bằng chứng bảo toàn

**117/117 file tracked sẵn có giữ nguyên SHA-256** trong audit. LTW22 so với `LTW22_backup_20261125`: **61/61 file nguồn/config ngoài bin/obj/Git/log khớp hash**.

LeaveManageDb chỉ được đọc, số lượng và `CHECKSUM_AGG(BINARY_CHECKSUM(*))` trước/sau khớp cả 9 bảng:

| Bảng | Số dòng | Checksum trước = sau |
|---|---:|---:|
| Departments | 4 | -333765515 |
| LeaveTypes | 5 | -1541723046 |
| Positions | 6 | -1248407890 |
| StoredFiles | 0 | NULL |
| Employees | 6 | 1437434056 |
| LeaveBalances | 12 | 15360 |
| LeaveRequests | 0 | NULL |
| Users | 6 | 260775136 |
| LeaveApprovals | 0 | NULL |

Count/checksum là bằng chứng phát hiện thay đổi, không phải fingerprint mật mã tuyệt đối của toàn DB. Các request có ghi chỉ dùng DB test. **LeaveManage_Test hiện giữ fixture tái hiện lỗi; không dùng DB này làm dữ liệu demo.** Chạy lại xUnit sẽ reset đúng DB test theo fixture.

## 3. Kết quả kiểm thử thực tế

| Kiểm tra | Kết quả độc lập |
|---|---|
| `dotnet restore LeaveManage.sln` | PASS |
| `dotnet build LeaveManage.sln -c Release --no-restore` | **0 warning, 0 error** |
| `dotnet test LeaveManage.sln -c Release --no-build --no-restore` | **20 passed, 0 failed, 0 skipped** |
| `dotnet ef migrations has-pending-model-changes --project LeaveManage.csproj --configuration Release --no-build` | Model khớp migration; không áp migration mới |
| `dotnet list LeaveManage.sln package --vulnerable --include-transitive` | Hai project không có package bị nguồn NuGet hiện tại báo vulnerable |
| HTTP bộ đầu | **84 kiểm tra mong đợi đạt** |
| HTTP bổ sung: hủy đồng thời/fixture | **7 kiểm tra mong đợi đạt**, có probe invariant riêng tìm được lỗi |
| Hai người duyệt cùng đơn | **200 và 409**, một approval, trừ phép một lần |
| Hai đơn trùng ngày gửi đồng thời | **200 và 409**, chỉ giữ chỗ một lần |
| Sáu lệnh hủy cùng một đơn Pending | **1 x 200, 5 x 409**, hoàn phép một lần, không có 500 |
| Sửa nháp đồng thời gửi duyệt | **FAIL invariant** ở lượt thử thứ 3: TotalDays 2 nhưng PendingDays chỉ tăng 1 |
| Manager bị khóa sử dụng JWT cũ | **FAIL bảo mật:** vẫn duyệt đơn thành Approved, HTTP 200 |
| Refresh một token bằng 5 request đồng thời | **FAIL tính dùng-một-lần:** 2, 3, rồi 4 request thành công trong ba lượt thử |
| Trang đầu và asset `/js/main.js`, `/js/core/api.js`, `/css/app.css` | HTTP 200 trên server audit |
| Swagger JSON | HTTP 200, **58 operations**, có cấu hình Bearer |
| JS syntax | **20/20** file trong `wwwroot/js` qua `node --check`; không thay thế browser test |
| Browser UI, responsive, console runtime | **NOT TESTED độc lập**: browser access host unavailable |
| Chạy lại Newman/Playwright/script smoke cũ của Claude | **NOT TESTED bằng chính runner/script cũ**; không nhận lại kết quả 28/37, 25/25, 41/41 làm kết quả audit |

**20 test xanh và 91 kiểm tra HTTP mong đợi đạt không có nghĩa toàn bộ hệ thống PASS.** Các probe đối kháng/cạnh tranh riêng đã chứng minh ba lỗi P1 và lỗi P2. Không gộp chúng thành một con số “tất cả đều đạt”.

Lần build đầu trong sandbox bị hủy với MSB5021; chạy lại với quyền thực thi phù hợp đạt 0/0. Đây không được quy thành lỗi source.

### Phạm vi HTTP đã xác minh

- Login Admin/Manager/User; anonymous 401, User/Manager vào API tài khoản Admin 403; anonymous register bị chặn.
- User thấy chính mình; Manager thấy bản thân + cấp dưới trực tiếp; User đọc nhân viên khác `/full` nhận 404.
- Tạo nhân viên/account/balance test; tài khoản liên kết login được; User không cấp balance.
- Draft/create/update, owner submit/cancel; người khác đọc/gửi/hủy 404; Manager không gửi đơn thay cấp dưới (403).
- Pending giữ phép; Approved chuyển Pending sang Used, không trừ hai lần; Cancel trước ngày bắt đầu trả Used; Reject có lý do và trả Pending; Draft cancel không giữ phép.
- Manager và Admin đều **không tự duyệt đơn chính mình**: 403 `SELF_APPROVAL`.
- Ngày ngược, cuối tuần, vắt năm, thiếu dữ liệu, query không hợp lệ trả 400. Thứ Sáu 11/12 đến thứ Hai 14/12 được tính **2 ngày làm việc**.
- Không cho giảm InitialDays dưới Used+Pending; không cho đổi `AllowsDeduction` của loại nghỉ đã tạo.
- Token hết hạn ký đúng và token bị sửa đều 401; refresh tuần tự xoay được, token cũ dùng lại tuần tự 401; logout chặn refresh; khóa chặn login/refresh. **Access JWT cũ lại không bị thu hồi**, xem P1-02.

## 4. Đúng đề tài, nguồn gốc LTW22 và kiến trúc

**Đúng đề tài do người dùng xác nhận:** “Thiết kế Back-end cho hệ thống quản lý ngày nghỉ của nhân sự”, nhóm 12. Domain hiện có Employee, Department, Position, LeaveType, LeaveRequest, LeaveBalance, LeaveApproval, User và StoredFile lab. Có nghiệp vụ nghỉ phép thật, không phải UniManage đổi nhãn.

Đối chiếu 61 file LTW22: 1 file byte-identical, 20 file chỉ khác sau chuẩn hóa tên LTW22 -> LeaveManage, 40 file thay đổi/không còn theo đường dẫn cũ. Cộng với các file nghiệp vụ mới và nội dung Auth/Models/Repository còn kế thừa, có căn cứ kỹ thuật rằng dự án **phát triển tiếp từ LTW22**, không chỉ tuyên bố trong README. Không coi số file giống nhau là thước đo điểm số.

| Ưu tiên | Kết luận |
|---|---|
| Controller-Service-Repository | Đúng tổ chức cơ bản; HTTP ở Controller, nghiệp vụ/scope ở Service, EF/transaction ở Repository |
| Không context trong Controller/Service | Quét không thấy AppDbContext/DbSet/IQueryable/EF SQL trực tiếp. Program/Data dùng DbContext để DI/migrate/seed là đúng tầng, không vi phạm |
| DI | Repository/service scoped; PasswordService singleton nhưng stateless, tạo HMAC mới mỗi lần gọi; không thấy scoped dependency trong singleton này |
| DTO/validation | Có DataAnnotations và service rule; self request không nhận EmployeeId, không cho User tự chọn chủ đơn; response không xuất hash/salt |
| Authorization | Admin quản trị; Manager trực tiếp phê duyệt; User thao tác đơn mình. Các rule hoạt động với token còn đúng trạng thái, nhưng thiếu tái kiểm tra account khi JWT đã cấp |
| Transaction | ReadCommitted + `UPDLOCK, ROWLOCK` ở dòng Employee. Hiệu lực phụ thuộc mọi writer cùng tuân thủ; UpdateDraftAsync đang đứng ngoài giao thức này |
| Database/migration | Unique balance Employee+Type+Year, approval theo Request; FK, date/nonnegative check. Test migrate từ DB rỗng đã chạy thành công |
| Seed | Danh mục/employee/balance cố định trong migration; account runtime dùng cùng PasswordService; không ghi đè account đã tồn tại; năm mới có bổ sung balance khi bật seed |
| UI gọi API thật | Đã rà fetch, Bearer/refresh, route/page request; endpoint thực có phản hồi. Không thấy mock KPI thay backend. Chưa xác minh rendering/click trực tiếp |
| Demo máy hiện tại | Release server + SQL LocalDB chạy được tại cổng test. Cổng demo 7103 chưa chạy ở thời điểm probe; GET trang/Swagger tại đó lỗi kết nối. Không suy ra app không thể chạy: cần nhóm khởi động đúng profile trước demo |

Các endpoint lab Files/Errors/Validation/System được giữ lại, có policy. Chưa kiểm thử upload thật hoặc mọi nhánh lab; không gắn upload thành chức năng đính kèm đơn nghỉ nếu chưa làm. Endpoint HR legacy là policy riêng, không nên tuyên bố mọi endpoint legacy đều áp DataScope như luồng nghỉ phép.

## 5. Các vấn đề cần chuyển Claude

### P1-01 - Sửa nháp/gửi duyệt cạnh tranh làm sai số dư, kẹt đơn

**Code:** `Services/LeaveRequestService.cs:108-125` đọc Draft, kiểm tra status rồi sửa/lưu **không transaction/LockEmployee**. Submit ở dòng 130 trở đi và approve/cancel lại khóa Employee qua `Repositories/LeaveRepository.cs`.

**Tái hiện thực tế trên LeaveManage_Test:**

1. Nhân viên test ID **7**, account test mới, phép năm Initial=10, Used=0, Pending=0.
2. Tạo đơn Draft 07/12/2026, một ngày.
3. Gửi cùng lúc `PUT /api/leave-requests/{id}` sửa thành 07-08/12 và `POST /api/leave-requests/{id}/submit`.
4. Lượt 1-2 đúng; **lượt 3**, request ID **22**: cả hai HTTP 200, status Pending, TotalDays=2, nhưng PendingDays=1.
5. Đọc SQL trực tiếp: `PendingDays=1`, tổng TotalDays của đơn Pending cùng employee/type/year = **2**.
6. Manager approve và chủ đơn cancel đều **409 CONSTRAINT_VIOLATION** vì cố trừ 2 khỏi Pending=1. Transaction rollback giữ nguyên trạng thái sai; không thể hoàn tất/hủy bình thường.

**Nguyên nhân:** check Draft rồi ghi không nguyên tử với submit. Khóa ở submit không bảo vệ writer không lấy cùng khóa; không có rowversion/conditional write thay thế.

**Sửa tối thiểu đề xuất:** UpdateDraft dùng cùng transaction + khóa Employee như mọi mutation đơn/số dư; **sau khi lấy khóa mới nạp lại entity, kiểm tra owner và Draft, validate rồi lưu**. Không chỉ đặt try/catch hoặc map exception thành 409 vì lỗi là sai dữ liệu trước đó. Không cần đổi cấu trúc dự án hay đưa context vào Service.

**Regression bắt buộc:** PUT đổi TotalDays/LeaveType đồng thời submit. Kết quả hợp lệ: edit thắng trước thì submit giữ đúng ngày mới; hoặc submit thắng thì edit bị từ chối. Không chấp nhận PendingDays lệch, và approve/cancel sau đó phải hoạt động. Chạy nhiều lượt và đối chiếu tổng Pending/Approved với balance.

### P1-02 - Khóa tài khoản không ngăn JWT đã cấp tiếp tục phê duyệt

**Code:** `Program.cs` AddJwtBearer chỉ xác minh chữ ký/issuer/audience/lifetime, không có OnTokenValidated kiểm tra account hiện tại. `Services/CallerContext.cs` dùng role/EmployeeId từ JWT. `Services/UserService.cs:184-192` khóa account và xóa refresh; `Services/AuthService.cs:49-56` logout cũng chỉ xóa refresh.

**Tái hiện:** login Manager và giữ access JWT; Admin `PUT /api/users/{managerId}` với `isActive:false` -> 200; login mới/refresh -> 401 như mong đợi. Nhưng dùng **JWT Manager cũ** gọi approve đơn test ID **18** -> **200, Approved**. Tài khoản bị khóa vẫn có quyền ghi nghiệp vụ.

Đã kiểm tra thêm: sau logout, refresh bị 401 nhưng access JWT cũ vẫn GET danh sách đơn **200**. TTL access cấu hình **60 phút**, ClockSkew=0. Đây là JWT stateless còn hạn, không phải phá chữ ký. Đổi role/link/delete có nguy cơ claim cũ tương tự theo code, nhưng chưa tái hiện riêng từng trường hợp.

**Sửa ưu tiên:** kiểm tra account tồn tại/IsActive và tính còn hiệu lực của quyền/EmployeeId tại bước validate access token hoặc authorization, gọi qua service/repository. Với logout/đổi mật khẩu/quyền, chọn cơ chế version/nonce/session phù hợp để vô hiệu token cũ; không chỉ kiểm tra refresh hash khác null vì login mới có thể làm JWT cũ sống lại. Không cần xây cả hệ thống phiên nhiều thiết bị trước kỳ thi.

**Regression:** JWT lấy trước khóa phải bị chặn khi GET và approve; refresh/login vẫn chặn. Xác định rõ contract logout (hiện chỉ thu hồi refresh); nếu sửa thành logout thu hồi access thì token trước logout phải không hoạt động kể cả sau login mới. Không gọi hành vi hiện tại là “khóa chặn toàn bộ truy cập”.

### P1-03 - Refresh rotation không nguyên tử, token cũ dùng đồng thời nhiều lần

**Code:** `Services/AuthService.cs:37-46,59-70`; `Repositories/AuthRepository.cs:22-25`; `Models/User.cs` không có concurrency/version cho rotation. Đọc hash rồi gán hash mới/lưu là hai giai đoạn, không compare-and-swap.

**Tái hiện:** login account test, gửi **5 POST /api/users/refresh đồng thời với cùng một refreshToken**. Ba lượt thử có lần lượt **2, 3, 4 response 200**, còn lại 401. Mỗi response 200 trả cặp access/refresh **khác nhau**. Không in token vào báo cáo.

Refresh tuần tự vẫn 200 rồi token cũ 401. DB chỉ giữ hash được ghi sau cùng; **không kết luận tất cả refresh token trả về đều còn dùng được**, nhưng các access JWT đã phát vẫn hợp lệ theo cấu hình hiện tại. Có thể mất phiên ngẫu nhiên và không đảm bảo tính dùng-một-lần/replay protection khi cạnh tranh. Frontend single-flight chỉ trong một JS context không sửa được lỗi server.

**Sửa tối thiểu:** tiêu thụ/rotate token nguyên tử tại repository, kiểm tra hash cũ + expiry/account active trong điều kiện UPDATE/transaction, chỉ một request thắng; request thua không phát JWT. Phối hợp cùng lock/logout để không tái cấp phiên vừa thu hồi. Không bắt buộc thêm bảng refresh-session lớn.

**Regression:** 5-10 request cùng token -> đúng một 200, còn lại 401 hoặc contract conflict rõ; refresh mới thắng dùng được; token cũ không dùng lại; thử refresh đua logout/khóa và không phát token hợp lệ sau revoke đã hoàn tất.

### P2-01 - Phân trang với page cực lớn trả 500

**Code:** `Repositories/LeaveRepository.cs:92,195`, `Repositories/EmployeeRepository.cs:56`, `Repositories/UserRepository.cs:60`; Page nhận int tới int.MaxValue rồi `(page-1)*pageSize` không được giới hạn/checked.

**Tái hiện HTTP:**

```text
GET /api/leave-requests?page=2147483647&pageSize=50 -> 500 INTERNAL_ERROR
GET /api/employees?page=2147483647&pageSize=100   -> 500 INTERNAL_ERROR
GET /api/users?pageNumber=2147483647&pageSize=50 -> 500 INTERNAL_ERROR
```

**Đề xuất nếu còn thời gian:** giới hạn page hợp lý và tính offset chống overflow, trả 400 cho giá trị vượt khả năng xử lý. Không dùng clamp giới hạn dưới để tuyên bố đã giải quyết toàn bộ page boundary. Không ảnh hưởng happy-path demo trang nhỏ.

### P3-01 - HMACSHA512 đúng cách triển khai lab, chưa phải password KDF cho vận hành thật

**Code:** `Services/PasswordService.cs`.

Điểm đúng: salt/key ngẫu nhiên riêng, không lưu plaintext; create/login/seed dùng chung thuật toán; FixedTimeEquals; HMAC object mới mỗi lần. DB test: **hash 64 byte, salt 128 byte, 8 account có 8 salt khác nhau**. Login account vừa tạo đã được kiểm chứng.

Giới hạn: HMAC một lần là phép tính nhanh, không có work factor/password stretching; salt không biến nó thành PBKDF2/Argon2. Nếu DB lộ, khả năng thử mật khẩu offline vẫn là rủi ro. Không nhầm SHA256 dùng để hash **refresh secret ngẫu nhiên 64 byte** với hash mật khẩu do người dùng chọn: hai bài toán khác nhau.

**Không yêu cầu đổi ngay trước 15:45.** Sau kỳ thi, nếu dùng thật, chuyển có kế hoạch sang password hasher phù hợp và migration/rehash, không làm tài khoản cũ mất khả năng login. [Microsoft khuyến nghị PasswordHasher cho ứng dụng lưu mật khẩu mới](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-10.0). Khi bảo vệ phải gọi đúng đây là lựa chọn theo lab, không tuyên bố đạt chuẩn production chỉ vì dùng SHA-512.

### P3-02 - Hardening ngoài môi trường demo

Access và refresh nằm trong sessionStorage; app hiện không có HTTPS redirection/HSTS hoặc rate limit login rõ trong Program. CORS allowlist không thay thế TLS, token storage cũng không chống được JS đã chạy cùng origin. Đây là giới hạn cho triển khai thật, **không mở thêm hạng mục triển khai trước bảo vệ**. Demo dùng HTTPS localhost; không public Internet với dữ liệu nhân sự thật.

## 6. Đối chiếu Buổi 1-11

Đã đọc các trang chủ đề/thực hành liên quan trong 11 PDF bài giảng; xem trực quan trang kiến trúc S7 tr.17. Không lấy file câu hỏi cũ hay thiết kế UniManage làm rubric của đề tài này.

| Buổi | Kiến thức và bằng chứng LeaveManage |
|---|---|
| 1 | C# class/interface, record, LINQ, async/await: Models, CallerContext/DataScope, Repository và Service |
| 2 | DI/middleware/hosting: Program; scoped repo/service, singleton stateless PasswordService; auth trước authorization |
| 3 | Controller/routing/binding/HTTP: DTO body, query, route ID, 201/200/204/400/401/403/404/409; action submit/approve/cancel mô tả transition |
| 4 | DTO, annotations, Swagger/Postman, custom MustBeEven; validation lỗi được thực thi qua API. Không đồng nhất mọi lỗi thành một JSON schema: framework ModelState vẫn ProblemDetails |
| 5 | AppDbContext, SQL provider, Code First/migration; test dựng DB trống và pending-model-check đạt |
| 6 | EF CRUD, Include, quan hệ 1-n, self-reference, balance Employee-Type-Year có thuộc tính, account 0..1 |
| 7 | Controller -> Service -> Repository, DI và mapping; không context trực tiếp trong Controller/Service |
| 8 | Login/JWT/refresh; có storage hash ngẫu nhiên và expiry. Flow cơ bản đạt nhưng rotation cạnh tranh và revocation còn lỗi |
| 9 | Admin/Manager/User, Claim EmployeeId, policy/HR/custom age; 401 vs 403/404; direct-report/self-approval được kiểm tra |
| 10 | Giữ upload/Options Pattern từ LTW22, signature/size/extension validation có trong source; chưa gắn file vào đơn nghỉ và chưa chạy upload trong audit |
| 11 | Pagination/search/filter; users có sort switch, request có thứ tự mặc định ổn định. Không nói mọi màn hình có sorting động; còn lỗi overflow ở page cực lớn |

Transaction/UPDLOCK và các test đối kháng là phần chuyên môn bổ sung giúp kiểm chứng nghiệp vụ, không phải căn cứ yêu cầu đổi sang Clean Architecture, thêm module HR khác hoặc mở rộng đề tài.

## 7. Security, tài liệu và giới hạn xác minh còn lại

- Refresh secret 64 byte CSPRNG, SHA256 hex trong DB, expiry 7 ngày theo cấu hình; không lưu refresh plaintext tại server. Giới hạn một refresh hash/account: login ở client khác ghi đè hash trước, chưa phải đa phiên thiết bị.
- JWT ký HS256, kiểm tra issuer/audience/lifetime/key. Không có cơ chế kiểm tra trạng thái account mỗi request; đây là lý do P1-02, không phải vấn đề thư viện JWT.
- Ownership trong core request/balance/dashboard có ở server. Admin cũng bị chặn tự duyệt, Manager không được duyệt ngoài trực thuộc theo test. Không nên chỉ dựa vào ẩn menu.
- Quét **4 commit reachable**, đối chiếu **2 secret thật local đang cấu hình**: **0 match**; `demo-accounts.local.txt` không tracked. Không tuyên bố audit được mọi secret chưa biết hay object Git unreachable.
- NuGet không có vulnerability được feed hiện tại báo; không phải chứng nhận không còn lỗ hổng toàn hệ thống hoặc mọi JS vendor.
- API client frontend có fetch thật, tự Bearer, refresh một lần/single-flight, modal/state/error. Trang và JS/CSS trả 200; browser access host unavailable nên **không xác nhận lại 25/25 UI của Claude**.
- Newman/Playwright/smoke cũ là kết quả được Claude ghi trong TEST_RESULTS; audit không tự gán PASS nếu chưa chạy đúng runner. Mục tiêu ưu tiên đã được kiểm bằng xUnit + HTTP độc lập.
- Không thử tải lớn, mọi race có thể có, production deploy hoặc máy khác. Không sửa source để phục vụ test.

Tài liệu hiện mô tả đúng phần lớn MVP (ngày làm việc T2-T6, không ngày lễ, 1 cấp duyệt, không carry-forward, Expired chưa có job). Những giới hạn này không phải P1 cần thêm tính năng. Riêng câu “rotation ngăn reuse” cần nói rõ mới đúng với tuần tự; kết quả concurrency mới phải được bổ sung sau khi sửa. “Khóa account không login/refresh” đúng, nhưng không có nghĩa token cũ không dùng được.

Không thực hiện hướng dẫn drop database demo trong DEMO_SCRIPT. Trước buổi bảo vệ chỉ khởi động, kiểm tra connection/credential riêng tại máy và luyện demo; **không chiếu `dotnet user-secrets list` hay file credential lên màn hình**.

## 8. Bàn giao ngay

1. Chuyển ba P1 ở mục 5 cho Claude, sửa có giới hạn trong kiến trúc hiện tại.
2. Chạy lại 20 test và thêm regression cho đúng ba lỗi, không chỉ chạy lại suite cũ.
3. Tự chạy một lượt UI create -> submit -> approve -> cancel, số dư trước/sau, role/ownership và refresh/logout; không bấm gửi/sửa song song để che bug thay cho sửa.
4. Dùng [FINAL_EXAM_READINESS.md](FINAL_EXAM_READINESS.md) để ôn và chốt demo trước 15:45. Không tiếp tục audit những cải tiến không cần thiết.
