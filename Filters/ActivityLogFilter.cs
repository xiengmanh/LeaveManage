using System.Security.Claims;
using LeaveManage.Exceptions;
using LeaveManage.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;

namespace LeaveManage.Filters;

/// <summary>
/// Ghi nhật ký cho mọi thao tác thay đổi dữ liệu (POST/PUT/PATCH/DELETE) của người đã đăng nhập.
/// Đăng nhập được ghi riêng trong UsersController vì lúc đó chưa có danh tính.
/// </summary>
public class ActivityLogFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> Mutating = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    // Controller.Action → mô tả tiếng Việt
    private static readonly Dictionary<string, string> Actions = new()
    {
        ["Users.Register"] = "Admin cấp tài khoản",
        ["Users.Update"] = "Cập nhật tài khoản",
        ["Users.Delete"] = "Xóa tài khoản",
        ["Users.Logout"] = "Đăng xuất",
        ["Users.ChangePassword"] = "Đổi mật khẩu",
        ["Users.UpdatePhone"] = "Đổi số điện thoại",
        ["LeaveRequests.Create"] = "Tạo đơn nghỉ",
        ["LeaveRequests.UpdateDraft"] = "Sửa đơn nghỉ nháp",
        ["LeaveRequests.Submit"] = "Gửi duyệt đơn nghỉ",
        ["LeaveRequests.Approve"] = "Duyệt đơn nghỉ",
        ["LeaveRequests.Reject"] = "Từ chối đơn nghỉ",
        ["LeaveRequests.Cancel"] = "Hủy đơn nghỉ",
        ["LeaveBalances.Create"] = "Cấp số dư phép",
        ["LeaveBalances.Update"] = "Cập nhật số dư phép",
        ["Employees.Create"] = "Thêm nhân viên",
        ["Employees.Update"] = "Cập nhật nhân viên",
        ["Employees.Delete"] = "Cho nhân viên nghỉ việc",
        ["Departments.Create"] = "Thêm phòng ban",
        ["Departments.Update"] = "Sửa phòng ban",
        ["Departments.Delete"] = "Xóa phòng ban",
        ["Positions.Create"] = "Thêm chức vụ",
        ["Positions.Update"] = "Sửa chức vụ",
        ["Positions.Delete"] = "Xóa chức vụ",
        ["LeaveTypes.Create"] = "Thêm loại nghỉ",
        ["LeaveTypes.Update"] = "Sửa loại nghỉ",
        ["LeaveTypes.Delete"] = "Xóa loại nghỉ"
    };

    // Không ghi: làm mới token (ồn ào, không phải hành động của người dùng) và các endpoint minh họa
    private static readonly HashSet<string> Skipped = new() { "Users.Refresh", "Validation.ValidateEvenNumber" };

    private readonly IActivityLogService _log;

    public ActivityLogFilter(IActivityLogService log) => _log = log;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        var http = context.HttpContext;
        if (!Mutating.Contains(http.Request.Method) || http.User.Identity?.IsAuthenticated != true) return;
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor) return;

        var key = $"{descriptor.ControllerName}.{descriptor.ActionName}";
        if (Skipped.Contains(key) || descriptor.ControllerName is "Errors" or "Validation") return;

        var action = Actions.TryGetValue(key, out var text) ? text : $"{http.Request.Method} {http.Request.Path}";
        if (http.Request.RouteValues.TryGetValue("id", out var id) && id is not null) action += $" #{id}";

        var status = executed.Exception switch
        {
            AppException app => app.StatusCode,
            not null => StatusCodes.Status500InternalServerError,
            _ => executed.Result switch
            {
                ObjectResult { StatusCode: int code } => code,
                ObjectResult => StatusCodes.Status200OK,
                Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult { StatusCode: int code } => code,
                _ => StatusCodes.Status200OK
            }
        };

        var userId = int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        await _log.LogAsync(userId, http.User.FindFirstValue(ClaimTypes.Name) ?? "?", action,
            http.Request.Method, http.Request.Path, status, http.Connection.RemoteIpAddress?.ToString());
    }
}
