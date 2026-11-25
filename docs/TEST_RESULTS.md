# Kết quả kiểm thử — LeaveManage

Ngày chạy: 25/11/2026, trên laptop demo (Windows 11, .NET SDK 10, SQL Server LocalDB).
Mã nguồn được kiểm thử: commit `fce70a4` (đã sửa P1-01, P1-02, P1-03, P2-01 theo [CODEX_FINAL_AUDIT.md](CODEX_FINAL_AUDIT.md)).
**Mọi mục ghi PASS đều đã chạy thật.** Không có mục nào được suy đoán.

**Database:**
- Mọi kiểm thử có ghi dữ liệu chạy trên `LeaveManage_Test`. Database này được tạo lại trước mỗi lần chạy, chỉ xóa đúng tên này.
- `LeaveManageDb` (demo):
  - được **sao lưu** trước khi áp migration `AddUserTokenVersion` (chỉ thêm cột);
  - sau migration, số dòng và checksum của 8 bảng nghiệp vụ không đổi;
  - đăng nhập thử 6 tài khoản demo (thao tác này ghi refresh token như khi dùng thật);
  - vẫn **0 đơn nghỉ, 6 tài khoản**.

## 1. Tổng hợp (sau khi sửa)

| # | Hạng mục | Công cụ | Kết quả |
|---|---|---|---|
| 1 | Build Release toàn solution | `dotnet build LeaveManage.sln -c Release` | **PASS**: 0 Warning, 0 Error |
| 2 | Model khớp migration | `dotnet ef migrations has-pending-model-changes` | **PASS**: không có thay đổi chưa tạo migration |
| 3 | xUnit: 20 test cũ + 15 regression test mới | `dotnet test` (WebApplicationFactory, SQL Server thật) | **PASS 35/35**, chạy 12 lần sau khi sửa (Release 4, Debug 8), lần nào cũng 35/35 |
| 4 | Regression test trên **bản cũ 99955c9** (bằng chứng "trước") | Cùng file `RegressionTests.cs`, chạy trên mã `git archive 99955c9` ở thư mục tạm | **14/15 FAIL** (đúng các lỗi audit; test đạt duy nhất là "trang hợp lệ cuối trả danh sách rỗng") |
| 5 | Smoke test HTTP 41 bước (bản Release) | Script Python gọi API thật | **PASS 41/41** |
| 6 | Postman collection (bản Release) | Newman, `postman/LeaveManage.postman_collection.json` | **PASS**: 28 request, 37 assertion, 0 lỗi |
| 7 | Giao diện trên Chrome thật (bản Release) | Playwright headless, 27 kiểm tra (thêm U21, U22 cho đăng xuất/khóa) | **PASS 27/27**, không có lỗi JavaScript |
| 8 | Migration trên `LeaveManageDb` | Sao lưu `BACKUP ... WITH CHECKSUM` + `RESTORE VERIFYONLY` → `dotnet ef database update` | **PASS**: backup hợp lệ; 8 bảng giữ nguyên số dòng và checksum; cột `TokenVersion` = 0 cho 6 tài khoản |
| 9 | Đăng nhập tài khoản demo sau migration (profile https, `LeaveManageDb`) | curl tới https://localhost:7103 | **PASS**: admin, manager, nv003, nv004, nv005, nv006 đều login 200 và `/api/users/me` 200; trang web 200; Swagger 200 |
| 10 | Lỗ hổng gói NuGet | `dotnet list LeaveManage.sln package --vulnerable --include-transitive` | **PASS**: cả 2 project không có gói lỗ hổng |
| 11 | Bí mật trong repository | Tìm mật khẩu demo trong file được Git theo dõi/chưa ignore | **PASS**: không có. `demo-accounts.local.txt` và `backups/` nằm trong `.gitignore` |
| 12 | Thư mục được bảo vệ | So sánh LTW22 với bản sao lưu; `git status` UniManage; 2 báo cáo audit | **PASS**: LTW22 giống hệt bản sao lưu; UniManage 0 thay đổi (`c18924e`); 2 báo cáo audit giữ nguyên nội dung gốc |

## 2. Regression test cho các lỗi audit ([RegressionTests.cs](../tests/LeaveManage.Tests/RegressionTests.cs))

| Test | Lỗi | Nội dung | Bản cũ 99955c9 | Bản sửa |
|---|---|---|---|---|
| `P1_01_EditDaysWhileSubmitting_BalanceAlwaysMatchesRequest` | P1-01 | 15 lượt: sửa 1→2 ngày **đồng thời** gửi duyệt. Mỗi lượt kiểm tra: PendingDays = TotalDays của đơn; sửa thắng ⇒ 2 ngày, gửi thắng ⇒ sửa bị 409; sau đó duyệt (Used = TotalDays) và hủy (về 0) | FAIL: TotalDays 2, PendingDays 1 | PASS |
| `P1_01_EditLeaveTypeWhileSubmitting_PendingOnCorrectBalance` | P1-01 | 15 lượt: đổi Phép năm → Phép ốm **đồng thời** gửi duyệt. Giữ chỗ đúng số dư của loại nghỉ cuối cùng; loại kia = 0; xen kẽ duyệt+hủy / hủy | FAIL: giữ chỗ sai loại nghỉ | PASS |
| `P1_02_LockedManager_OldJwtCannotReadOrApprove_AndDoesNotReviveAfterUnlock` | P1-02 | Manager đăng nhập → Admin khóa → JWT cũ đọc và duyệt đều **401**, đơn vẫn Chờ duyệt; login/refresh 401; mở khóa → JWT cũ **vẫn 401**; đăng nhập mới duyệt được | FAIL: JWT cũ vẫn 200 | PASS |
| `P1_02_Logout_RevokesAccessToken_EvenAfterNewLogin` | P1-02 | Sau logout: JWT cũ 401, refresh cũ 401; đăng nhập mới dùng được, JWT cũ vẫn 401 | FAIL | PASS |
| `P1_02_RoleOrEmployeeLinkChange_InvalidatesOldJwt` | P1-02 | Đổi role User → Manager: JWT cũ 401, đăng nhập lại có role mới. Đổi nhân viên liên kết: JWT cũ 401, đăng nhập lại có EmployeeId mới | FAIL | PASS |
| `P1_03_ConcurrentRefreshWithSameToken_ExactlyOneWins` | P1-03 | 5 lượt × **8 refresh đồng thời** cùng token: đúng 1 lần 200, còn lại 401; token mới dùng được; token cũ 401 | FAIL: 2 request thắng | PASS |
| `P1_03_RefreshRacingLogout_NoUsableTokenSurvives` | P1-03 | 8 lượt refresh **đua** logout: sau khi xong, không token nào (cũ hay mới) dùng được | FAIL | PASS |
| `P1_03_RefreshRacingAccountLock_NoUsableTokenSurvives` | P1-03 | 5 lượt refresh **đua** khóa tài khoản: không token nào dùng được; login 401 | FAIL | PASS |
| `P2_01_HugePageNumber_Returns400` (6 URL) | P2-01 | `page=2147483647` / `100001` trên leave-requests, pending-approval, employees, users, leave-approvals → **400** | FAIL: 500 | PASS |
| `P2_01_LastAllowedPage_ReturnsEmptyPage` | P2-01 | `page=100000` → 200, danh sách rỗng | PASS | PASS |

Ghi chú về cách cô lập dữ liệu:
- Các regression test tự tạo nhân viên và tài khoản riêng, quản lý trực tiếp là NV001 (admin), để không ảnh hưởng dữ liệu của test khác.
- Test phạm vi của Admin trước đây kiểm tra "đúng 6 nhân viên"; nay đổi thành "có đủ 6 nhân viên mẫu NV001–NV006", vì Admin thấy mọi nhân viên, kể cả nhân viên do test khác tạo.

## 3. 20 test ban đầu (vẫn PASS)

| Test | Nội dung kiểm tra |
|---|---|
| `LeaveDayCalculatorTests` (4 trường hợp) | Đếm ngày: T2→T6 = 5; T6→T2 = 2; chỉ T7+CN = 0; 1 ngày = 1 |
| `Login_WrongPassword_Returns401` | Sai mật khẩu → 401 |
| `Refresh_RotatesToken_OldTokenRejected` | Refresh thành công; dùng lại refresh token cũ → 401 |
| `NoToken_401_UserOnAdminApi_403_AnonymousRegister_401` | Không token → 401; User gọi API Admin → 403; đăng ký ẩn danh → 401 |
| `AccountWithoutEmployee_CannotCreateLeave_LockedAccountCannotLogin` | Chưa gắn nhân viên → 403; gắn trùng → 409; Admin tự khóa → 400; tài khoản bị khóa → login 401 |
| `EmployeeList_IsScopedByRole` | User thấy [3]; Manager thấy [2,3,4,6]; Admin thấy đủ NV001–NV006 |
| `User_CannotReadOtherEmployee_Returns404` | Xem nhân viên khác → 404 (cả `/full`) |
| `FullFlow_Submit_Approve_Cancel_UpdatesBalanceCorrectly` | Gửi → duyệt → duyệt lại 409 → hủy; số dư đúng từng bước |
| `Reject_ReleasesPendingDays_RequiresComment` | Từ chối không lý do → 400; từ chối trả PendingDays |
| `Validation_DateRange_Notice_WeekendOnly_CrossYear` | Ngày ngược, không báo trước, chỉ cuối tuần, vắt năm, nghỉ cưới > 3 ngày → 400 |
| `Overlap_WithPendingRequest_Returns409` | Trùng ngày → 409 `OVERLAPPING_REQUEST` |
| `InsufficientBalance_Returns409` | Số dư 0 → 409 `INSUFFICIENT_BALANCE` |
| `Ownership_OtherUserCannotSubmitOrCancel_ManagerOnlyDirectReports` | Người khác gửi/hủy → 404; Manager gửi đơn cấp dưới → 403; duyệt đơn không phải cấp dưới → 404 |
| `Concurrency_TwoApproversAtOnce_OnlyOneSucceeds_BalanceDeductedOnce` | 2 lệnh duyệt song song → [200, 409]; trừ số dư 1 lần |
| `Concurrency_TwoOverlappingSubmitsAtOnce_OnlyOneSucceeds` | 2 lần gửi trùng ngày song song → [200, 409] |
| `Catalog_DuplicateCode409_UserCannotCreate403` | Trùng mã → 409; User tạo loại nghỉ → 403; Admin tạo chức vụ → 201 |
| `Dashboard_ReturnsScopedCounts` | Dashboard Manager: 4 nhân viên trong phạm vi |

## 4. Kiểm thử giao diện (27 kiểm tra, Chrome headless, bản Release)

| Mã | Kiểm tra | Kết quả |
|---|---|---|
| U01–U03 | Đăng nhập sai báo lỗi tiếng Việt; menu theo role; form trống báo lỗi dưới ô nhập | PASS |
| U04–U07 | User: tạo nháp → gửi duyệt → xem chi tiết; vào `/approvals` bị chặn | PASS |
| U08–U11 | Manager: thấy đơn chờ của cấp dưới → duyệt → đơn "Đã duyệt"; thấy 4 nhân viên | PASS |
| U12–U14 | Admin mở Nhân viên, Số dư, Lịch sử, Tài khoản, 3 danh mục; trùng mã → 409 trong form | PASS |
| U15–U16 | Mobile 390px: menu offcanvas, không tràn ngang | PASS |
| U17 | Access token hỏng → tự refresh, vẫn ở trang | PASS |
| U18–U19 | Swagger JSON 200; header CSP | PASS |
| **U21** (mới) | Đăng xuất trên giao diện → JWT cũ gọi API trả 401 | PASS |
| **U22** (mới) | Admin khóa tài khoản đang đăng nhập → thao tác tiếp theo đưa về trang đăng nhập | PASS |
| U20 | Không có lỗi JavaScript trên console | PASS |

Ghi chú U20:
- Lần chạy đầu sau khi sửa, U20 **FAIL**. Trang đang tải khi phiên bị thu hồi vẫn ghi `console.error` và hiện thêm thông báo "Không thể hiển thị trang", dù 401 đã được xử lý bằng sự kiện `auth:expired`.
- Đã sửa trong `wwwroot/js/main.js` (bỏ qua lỗi 401 đã xử lý). Chạy lại thì PASS.
- Log mạng "Failed to load resource 401/409" do test cố ý tạo không tính là lỗi JavaScript.

## 5. Chưa kiểm thử

- Hiệu năng/tải lớn (mỗi request xác thực thêm 1 truy vấn theo khóa chính).
- Trình duyệt khác Chrome; máy khác ngoài laptop demo.
- Các chức năng lab giữ từ LTW22 (upload file, `/api/Errors`, `/api/Validation`, `/api/System`): không có test tự động mới.
- Khôi phục database từ bản sao lưu: chỉ kiểm tra `RESTORE VERIFYONLY`, không thực sự restore đè lên `LeaveManageDb`.
