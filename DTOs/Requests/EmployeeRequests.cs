using LeaveManage.Constants;
using System.ComponentModel.DataAnnotations;

namespace LeaveManage.DTOs.Requests;

// ==================== EMPLOYEE REQUEST DTOs ====================

public class EmployeeQueryRequest
{
    [Range(1, PagingLimits.MaxPage)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// DTO tạo nhân viên mới
/// </summary>
public class CreateEmployeeRequest
{
    [Required(ErrorMessage = "Mã nhân viên không được để trống")]
    [StringLength(50, ErrorMessage = "Mã nhân viên tối đa 50 ký tự")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
    public string Email { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Ngày sinh không được để trống")]
    public DateTime DateOfBirth { get; set; }

    [Required(ErrorMessage = "Ngày vào làm không được để trống")]
    public DateTime HireDate { get; set; }

    [Required(ErrorMessage = "Phòng ban không được để trống")]
    [Range(1, int.MaxValue, ErrorMessage = "Phòng ban không hợp lệ")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "Chức vụ không được để trống")]
    [Range(1, int.MaxValue, ErrorMessage = "Chức vụ không hợp lệ")]
    public int PositionId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Manager không hợp lệ")]
    public int? ManagerId { get; set; }
}

/// <summary>
/// DTO cập nhật nhân viên
/// </summary>
public class UpdateEmployeeRequest
{
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string? FullName { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? PhoneNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Phòng ban không hợp lệ")]
    public int? DepartmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Chức vụ không hợp lệ")]
    public int? PositionId { get; set; }

    /// <summary>0 = bỏ quản lý trực tiếp.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Manager không hợp lệ")]
    public int? ManagerId { get; set; }

    public bool? IsActive { get; set; }
}
