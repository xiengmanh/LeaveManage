using System.Security.Claims;
using LeaveManage.Constants;
using LeaveManage.Exceptions;

namespace LeaveManage.Services;

/// <summary>
/// Thông tin người gọi API, đọc từ JWT. Controller tạo ra và truyền xuống Service
/// để Service tự kiểm tra quyền sở hữu (ownership) và phạm vi dữ liệu.
/// </summary>
public sealed record CallerContext(int UserId, string Role, int? EmployeeId)
{
    public bool IsAdmin => Role == UserRoles.Admin;
    public bool IsManager => Role == UserRoles.Manager;

    /// <summary>Nhân viên gắn với tài khoản; nếu chưa gắn thì không dùng được chức năng cá nhân.</summary>
    public int RequireEmployeeId() => EmployeeId
        ?? throw new ForbiddenException("Tài khoản chưa được gắn với nhân viên nào.", "ACCOUNT_NOT_LINKED");

    public static CallerContext From(ClaimsPrincipal user)
    {
        var userId = int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var role = user.FindFirstValue(ClaimTypes.Role) ?? UserRoles.User;
        int? employeeId = int.TryParse(user.FindFirstValue(CustomClaimTypes.EmployeeId), out var eid) ? eid : null;
        return new CallerContext(userId, role, employeeId);
    }
}

/// <summary>
/// Phạm vi dữ liệu được xem: AllEmployees = Admin; ngược lại chỉ EmployeeId của mình
/// (và nhân viên cấp dưới trực tiếp nếu IncludeDirectReports = true, dành cho Manager).
/// </summary>
public sealed record DataScope(bool AllEmployees, int EmployeeId, bool IncludeDirectReports)
{
    public static DataScope For(CallerContext caller)
    {
        if (caller.IsAdmin)
            return new DataScope(true, caller.EmployeeId ?? 0, false);
        return new DataScope(false, caller.RequireEmployeeId(), caller.IsManager);
    }
}
