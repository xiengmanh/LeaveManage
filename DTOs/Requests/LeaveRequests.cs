using System.ComponentModel.DataAnnotations;
using LeaveManage.Models;

namespace LeaveManage.DTOs.Requests;

/// <summary>Tạo đơn nghỉ (trạng thái Draft). Người tạo là nhân viên gắn với tài khoản đang đăng nhập.</summary>
public class CreateLeaveRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Loại nghỉ không hợp lệ")]
    public int LeaveTypeId { get; set; }

    [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Ngày kết thúc là bắt buộc")]
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "Lý do là bắt buộc")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do từ 3 đến 500 ký tự")]
    public string Reason { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ContactAddress { get; set; }

    [StringLength(20)]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? ContactPhone { get; set; }
}

/// <summary>Sửa đơn khi còn ở trạng thái Draft.</summary>
public class UpdateLeaveRequestDto : CreateLeaveRequestDto
{
}

public class ApproveLeaveDto
{
    [StringLength(500)]
    public string? Comment { get; set; }
}

public class RejectLeaveDto
{
    [Required(ErrorMessage = "Phải nhập lý do từ chối")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do từ chối từ 3 đến 500 ký tự")]
    public string Comment { get; set; } = string.Empty;
}

public class CancelLeaveDto
{
    [StringLength(255)]
    public string? Reason { get; set; }
}

public class LeaveRequestQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;

    public LeaveRequestStatus? Status { get; set; }
    public int? EmployeeId { get; set; }
    public int? LeaveTypeId { get; set; }

    /// <summary>Lọc đơn có ngày nghỉ giao với khoảng [FromDate, ToDate].</summary>
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>Tìm theo mã đơn, mã hoặc tên nhân viên.</summary>
    [StringLength(100)]
    public string? Search { get; set; }
}

public class LeaveBalanceQuery
{
    [Range(2000, 2100)]
    public int? Year { get; set; }
    public int? EmployeeId { get; set; }
    public int? LeaveTypeId { get; set; }
}

public class CreateLeaveBalanceDto
{
    [Range(1, int.MaxValue)]
    public int EmployeeId { get; set; }

    [Range(1, int.MaxValue)]
    public int LeaveTypeId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(0, 366, ErrorMessage = "Số ngày được cấp từ 0 đến 366")]
    public int InitialDays { get; set; }
}

public class UpdateLeaveBalanceDto
{
    [Range(0, 366, ErrorMessage = "Số ngày được cấp từ 0 đến 366")]
    public int InitialDays { get; set; }
}

public class DepartmentUpsertDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PositionUpsertDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Range(1, 10)]
    public int Level { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

public class LeaveTypeUpsertDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    /// <summary>Số ngày tối đa mỗi năm (null = không giới hạn, ví dụ nghỉ không lương).</summary>
    [Range(1, 366)]
    public int? DefaultDaysPerYear { get; set; }

    [Range(0, 365)]
    public int? NoticeDaysRequired { get; set; }

    /// <summary>true = trừ vào số dư phép (LeaveBalance).</summary>
    public bool AllowsDeduction { get; set; }

    public bool IsActive { get; set; } = true;
}
