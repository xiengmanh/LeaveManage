namespace LeaveManage.DTOs.Responses;

public class LeaveApprovalResponse
{
    public int Id { get; set; }
    public int LeaveRequestId { get; set; }
    public string? RequestNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime? ActionDate { get; set; }
    public int ApproverId { get; set; }
    public string? ApproverName { get; set; }
}

public class LeaveRequestResponse
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeeName { get; set; }
    public int LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>Số ngày làm việc (thứ 2 – thứ 6) trong khoảng nghỉ.</summary>
    public int TotalDays { get; set; }
    public int PaidDays { get; set; }
    public string? Reason { get; set; }
    public string? ContactAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public List<LeaveApprovalResponse> Approvals { get; set; } = new();
}

public class LeaveBalanceResponse
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeeName { get; set; }
    public int LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public int Year { get; set; }
    public int InitialDays { get; set; }
    public int UsedDays { get; set; }
    public int PendingDays { get; set; }
    public int RemainingDays { get; set; }
}

public class LeaveTypeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DefaultDaysPerYear { get; set; }
    public int? NoticeDaysRequired { get; set; }
    public bool RequiresApproval { get; set; }
    public bool AllowsDeduction { get; set; }
    public bool IsActive { get; set; }
}

public class DashboardResponse
{
    public string Scope { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int CancelledCount { get; set; }
    /// <summary>Số đơn đang chờ mà người gọi có quyền duyệt.</summary>
    public int AwaitingMyApproval { get; set; }
    /// <summary>Số nhân viên trong phạm vi đang nghỉ (đơn Approved) hôm nay.</summary>
    public int OnLeaveToday { get; set; }
    public List<LeaveBalanceResponse> MyBalances { get; set; } = new();
}
