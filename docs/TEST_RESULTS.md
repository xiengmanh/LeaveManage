# Kết quả kiểm thử — LeaveManage

Ngày chạy: 25/11/2026, trên laptop demo (Windows 11, .NET SDK 10, SQL Server LocalDB).
**Mọi mục ghi PASS đều đã chạy thật.** Không có mục nào được suy đoán.

**Database kiểm thử:**
- Mọi kiểm thử tự động chạy trên `LeaveManage_Test`. Database này được xóa và tạo lại trước mỗi lần chạy, chỉ xóa đúng tên này.
- `LeaveManageDb` (demo) không bị ghi dữ liệu kiểm thử: sau khi chạy xong vẫn có 0 đơn nghỉ và 6 tài khoản demo.

## 1. Tổng hợp

| # | Hạng mục | Công cụ | Kết quả |
|---|---|---|---|
| 1 | Build Release toàn solution | `dotnet build LeaveManage.sln -c Release` | **PASS**: 0 Warning, 0 Error |
| 2 | Integration test + unit test | `dotnet test` (xUnit, WebApplicationFactory, SQL Server thật) | **PASS 20/20**, chạy 5 lần (Debug 4 lần, Release 1 lần), lần nào cũng 20/20 |
| 3 | Smoke test HTTP 41 bước (bản Release) | Script Python gọi API thật | **PASS 41/41** |
| 4 | Postman collection | Newman, `postman/LeaveManage.postman_collection.json` | **PASS**: 28 request, 37 assertion, 0 lỗi |
| 5 | Kiểm thử giao diện trên Chrome thật | Playwright (headless Chrome), 25 kiểm tra | **PASS 25/25**, không có lỗi JavaScript trên console |
| 6 | Migration trên database rỗng | `dotnet ef database update` → `LeaveManageDb`; test tự migrate `LeaveManage_Test` | **PASS** |
| 7 | Chạy demo bằng profile https | `dotnet run --launch-profile https` | **PASS**: trang web 200, Swagger 200, login admin/manager/nv003 đều 200 |
| 8 | Lỗ hổng gói NuGet | `dotnet list package --vulnerable --include-transitive` | **PASS**: không có gói lỗ hổng |
| 9 | Bí mật trong repository | Tìm mật khẩu demo / JWT Key trong file được Git theo dõi | **PASS**: không có. `demo-accounts.local.txt` nằm trong `.gitignore` |
| 10 | Thư mục được bảo vệ | So sánh `LTW22` với bản sao lưu; `git status` UniManage | **PASS**: LTW22 giống hệt bản sao lưu; UniManage không đổi (commit `c18924e`, 0 thay đổi) |

## 2. Chi tiết 20 test tự động ([tests/LeaveManage.Tests](../tests/LeaveManage.Tests))

| Test | Nội dung kiểm tra |
|---|---|
| `LeaveDayCalculatorTests` (4 trường hợp) | Đếm ngày: T2→T6 = 5; T6→T2 = 2; chỉ T7+CN = 0; 1 ngày = 1 |
| `Login_WrongPassword_Returns401` | Sai mật khẩu → 401 |
| `Refresh_RotatesToken_OldTokenRejected` | Refresh thành công; dùng lại refresh token cũ → 401 |
| `NoToken_401_UserOnAdminApi_403_AnonymousRegister_401` | Không token → 401; User gọi API Admin → 403; đăng ký ẩn danh → 401 |
| `AccountWithoutEmployee_CannotCreateLeave_LockedAccountCannotLogin` | Tài khoản chưa gắn nhân viên tạo đơn → 403; gắn nhân viên đã có tài khoản → 409; Admin tự khóa mình → 400; khóa tài khoản → không đăng nhập được (401) |
| `EmployeeList_IsScopedByRole` | User thấy [3]; Manager thấy [2,3,4,6]; Admin thấy 6 |
| `User_CannotReadOtherEmployee_Returns404` | Xem nhân viên khác → 404 (cả `/full`) |
| `FullFlow_Submit_Approve_Cancel_UpdatesBalanceCorrectly` | Gửi duyệt → còn lại −1; duyệt → không trừ thêm; có 1 bản ghi duyệt; duyệt lại → 409; hủy → trả lại số dư |
| `Reject_ReleasesPendingDays_RequiresComment` | Từ chối không lý do → 400; từ chối → trả lại PendingDays |
| `Validation_DateRange_Notice_WeekendOnly_CrossYear` | Ngày ngược → 400; không báo trước → 400; chỉ cuối tuần → 400; vắt năm → 400; nghỉ cưới > 3 ngày → 400 |
| `Overlap_WithPendingRequest_Returns409` | Trùng ngày với đơn Pending → 409 `OVERLAPPING_REQUEST` |
| `InsufficientBalance_Returns409` | Số dư 0 → 409 `INSUFFICIENT_BALANCE` |
| `Ownership_OtherUserCannotSubmitOrCancel_ManagerOnlyDirectReports` | Người khác gửi/hủy → 404; Manager gửi đơn của cấp dưới → 403; Manager duyệt đơn không phải cấp dưới → 404 |
| `Concurrency_TwoApproversAtOnce_OnlyOneSucceeds_BalanceDeductedOnce` | 2 lệnh duyệt song song → đúng [200, 409]; số dư trừ 1 lần; 1 bản ghi duyệt |
| `Concurrency_TwoOverlappingSubmitsAtOnce_OnlyOneSucceeds` | 2 lần gửi duyệt trùng ngày song song → đúng [200, 409] |
| `Catalog_DuplicateCode409_UserCannotCreate403` | Trùng mã phòng ban → 409; User tạo loại nghỉ → 403; Admin tạo chức vụ → 201 |
| `Dashboard_ReturnsScopedCounts` | Dashboard Manager: 4 nhân viên trong phạm vi, có số dư cá nhân |

## 3. Kiểm thử giao diện (25 kiểm tra, Chrome headless)

| Mã | Kiểm tra | Kết quả |
|---|---|---|
| U01 | Đăng nhập sai → thông báo tiếng Việt | PASS |
| U02 | Menu của User không có Duyệt đơn / Danh mục / Tài khoản | PASS |
| U03 | Gửi form trống → lỗi dưới từng ô nhập | PASS |
| U04–U06 | Tạo đơn nháp → Gửi duyệt (Chờ duyệt) → Xem chi tiết | PASS |
| U07 | User mở `/approvals` → trang "không có quyền" | PASS |
| U08–U10 | Manager thấy đơn chờ của cấp dưới → Duyệt → đơn hiện "Đã duyệt" trong danh sách | PASS |
| U11 | Manager thấy 4 nhân viên | PASS |
| U12 (×4) | Admin mở Nhân viên, Số dư, Lịch sử, Tài khoản | PASS |
| U13 (×3) | 3 tab danh mục có dữ liệu | PASS |
| U14 | Trùng mã phòng ban → lỗi 409 hiển thị trong form | PASS |
| U15–U16 | Mobile 390px: menu offcanvas mở được, không tràn ngang | PASS |
| U17 | Access token hỏng → tự refresh, vẫn ở trang | PASS |
| U18 | Swagger JSON (Development) trả 200 | PASS |
| U19 | Trang web có header Content-Security-Policy | PASS |
| U20 | Không có lỗi JavaScript trên console | PASS |

Ghi chú U20: trình duyệt tự ghi "Failed to load resource 401/409" cho các response mà test **cố ý** tạo (token hỏng, trùng mã). Đây là log mạng, không phải lỗi JavaScript, nên không tính.

## 4. Lỗi phát hiện trong quá trình kiểm thử và đã sửa

| Lỗi | Sửa |
|---|---|
| Đọc lại số dư sau khi khóa có thể lấy giá trị cũ do EF đã tracking entity từ trước | `LeaveBalanceService.UpdateAsync` đọc lần đầu bằng `AsNoTracking`, chỉ nạp entity tracking sau khi khóa |
| Admin có thể tự khóa / tự xóa / tự hạ quyền | API trả 400 `CANNOT_CHANGE_SELF` (có test) |
| Cảnh báo CS0108 trong project test | Đổi tên hằng số; build Release còn 0 warning |
| Test giao diện lần 2 bị 409 trùng ngày vì dữ liệu của lần chạy trước | Đây là hành vi đúng. Script được sửa để tạo lại database test trước mỗi lần chạy. |

## 5. Chưa kiểm thử

- Hiệu năng / tải lớn.
- Trình duyệt khác Chrome.
- Chạy trên máy khác ngoài laptop demo.
- Các chức năng lab giữ từ LTW22 (upload file, `/api/Errors`, `/api/Validation`, `/api/System`) chỉ kiểm tra qua build và Swagger, không có test tự động mới.
