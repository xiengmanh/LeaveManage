namespace LeaveManage.Constants;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    public static readonly HashSet<string> All =
        new(StringComparer.OrdinalIgnoreCase) { Admin, Manager, User };
}

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    /// <summary>Admin hoặc Manager.</summary>
    public const string CanManageEmployees = "CanManageEmployees";
    /// <summary>Admin hoặc Manager: được duyệt/từ chối đơn nghỉ.</summary>
    public const string CanApproveLeave = "CanApproveLeave";
    /// <summary>Tài khoản phải gắn với một nhân viên (có claim EmployeeId).</summary>
    public const string HasEmployee = "HasEmployee";
    public const string MustBeHR = "MustBeHR";
    public const string MinimumAge18 = "MinimumAge18";
}

public static class CustomClaimTypes
{
    public const string Department = "Department";
    public const string Age = "Age";
    public const string EmployeeId = "EmployeeId";
    public const string TokenVersion = "TokenVersion";
}
