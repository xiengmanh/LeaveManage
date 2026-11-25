using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveManage.Models;

// ==================== EMPLOYEE (NHÂN VIÊN) ====================

/// <summary>
/// Nhân viên trong hệ thống
/// </summary>
public class Employee
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    // Không lưu mật khẩu ở Employee: nhân viên đăng nhập bằng tài khoản User liên kết (User.EmployeeId).

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
    
    [Required]
    public DateTime DateOfBirth { get; set; }
    
    [Required]
    public DateTime HireDate { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Set by the application when a new employee is created.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }

    // ========== RELATIONSHIPS ==========

    [Required]
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    [Required]
    public int PositionId { get; set; }
    public Position? Position { get; set; }

    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public ICollection<LeaveApproval> Approvals { get; set; } = new List<LeaveApproval>();
}

// ==================== DEPARTMENT (PHÒNG BAN) ====================

/// <summary>
/// Phòng ban trong công ty
/// </summary>
public class Department : ICatalogEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;
    
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

// ==================== POSITION (CHỨC VỤ) ====================

/// <summary>
/// Chức vụ của nhân viên
/// </summary>
public class Position : ICatalogEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int Level { get; set; } = 1;
    
    public bool IsActive { get; set; } = true;
    
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

// ==================== LEAVE TYPE (LOẠI NGHỈ PHÉP) ====================

/// <summary>
/// Loại nghỉ phép (Annual Leave, Sick Leave, Maternity Leave, etc.)
/// </summary>
public class LeaveType : ICatalogEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? Description { get; set; }
    
    [Range(0, 365)]
    public int? DefaultDaysPerYear { get; set; }
    
    public bool RequiresApproval { get; set; } = true;
    
    [Range(0, 365)]
    public int? NoticeDaysRequired { get; set; }
    
    public bool AllowsDeduction { get; set; } = false;
    
    public bool IsActive { get; set; } = true;
    
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
}

// ==================== LEAVE REQUEST (ĐƠN XIN NGHỈ) ====================

/// <summary>
/// Đơn xin nghỉ phép của nhân viên
/// </summary>
public class LeaveRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;
    
    [Required]
    public int LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    [Range(0, 365)]
    public int TotalDays { get; set; }
    
    [Range(0, 365)]
    public int PaidDays { get; set; }
    
    [MaxLength(500)]
    public string? Reason { get; set; }
    
    [MaxLength(255)]
    public string? ContactAddress { get; set; }
    
    [MaxLength(20)]
    public string? ContactPhone { get; set; }
    
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Draft;
    
    public DateTime? SubmittedAt { get; set; }
    
    public DateTime? CancelledAt { get; set; }
    
    [MaxLength(255)]
    public string? CancelReason { get; set; }
    
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }

    [Required]
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    
    public ICollection<LeaveApproval> Approvals { get; set; } = new List<LeaveApproval>();
}

public enum LeaveRequestStatus
{
    Draft = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Expired = 5
}

// ==================== LEAVE BALANCE (SỐ NGÀY PHÉP) ====================

/// <summary>
/// Số ngày phép còn lại của nhân viên
/// </summary>
public class LeaveBalance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }
    
    [Range(0, 366)]
    public int InitialDays { get; set; }
    
    [Range(0, 366)]
    public int UsedDays { get; set; }
    
    [Range(0, 366)]
    public int PendingDays { get; set; }
    
[NotMapped]
    public int RemainingDays => InitialDays - UsedDays - PendingDays;
    
    [Range(0, 366)]
    public int CarriedForwardDays { get; set; }
    
    [Range(0, 366)]
    public int MaxCarryForwardDays { get; set; }
    
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }

    [Required]
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    
    [Required]
    public int LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
}

// ==================== LEAVE APPROVAL (PHÊ DUYỆT) ====================

/// <summary>
/// Lịch sử phê duyệt đơn nghỉ
/// </summary>
public class LeaveApproval
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Range(1, 10)]
    public int ApprovalLevel { get; set; } = 1;
    
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    
    public DateTime? ActionDate { get; set; }
    
    [MaxLength(500)]
    public string? Comment { get; set; }
    
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int LeaveRequestId { get; set; }
    public LeaveRequest? LeaveRequest { get; set; }
    
    [Required]
    public int ApproverId { get; set; }
    public Employee? Approver { get; set; }
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Skipped = 3
}
