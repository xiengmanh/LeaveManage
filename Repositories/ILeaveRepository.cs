using LeaveManage.DTOs.Requests;
using LeaveManage.Models;
using LeaveManage.Services;

namespace LeaveManage.Repositories;

/// <summary>
/// Truy cập dữ liệu cho nghiệp vụ nghỉ phép: LeaveRequest, LeaveBalance, LeaveApproval.
/// Service không dùng DbContext trực tiếp mà chỉ gọi qua interface này.
/// </summary>
public interface ILeaveRepository
{
    // ---------- Transaction + khóa ----------
    /// <summary>Chạy work trong một transaction; lỗi thì rollback toàn bộ.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work);

    /// <summary>
    /// Khóa dòng Employee (UPDLOCK) đến hết transaction. Mọi thao tác thay đổi đơn/số dư
    /// của cùng một nhân viên phải lấy khóa này trước, nên chạy tuần tự, không bị trừ phép 2 lần.
    /// </summary>
    Task LockEmployeeAsync(int employeeId);

    // ---------- Employee / LeaveType ----------
    Task<Employee?> GetEmployeeAsync(int employeeId);
    Task<LeaveType?> GetLeaveTypeAsync(int leaveTypeId);

    // ---------- LeaveRequest ----------
    Task<(List<LeaveRequest> Items, int TotalCount)> GetRequestsPagedAsync(LeaveRequestQuery query, DataScope scope, bool pendingForApprover = false);
    Task<LeaveRequest?> GetRequestDetailAsync(int id);
    Task<int?> GetRequestOwnerIdAsync(int id);
    Task<LeaveRequest?> GetRequestTrackedAsync(int id);
    Task<bool> HasOverlapAsync(int employeeId, DateTime start, DateTime end, int excludeRequestId);
    Task<int> SumDaysInYearAsync(int employeeId, int leaveTypeId, int year, int excludeRequestId);
    Task AddRequestAsync(LeaveRequest request);

    // ---------- LeaveBalance ----------
    Task<LeaveBalance?> GetBalanceTrackedAsync(int employeeId, int leaveTypeId, int year);
    Task<LeaveBalance?> GetBalanceTrackedByIdAsync(int id);
    Task<LeaveBalance?> GetBalanceByIdAsync(int id);
    Task<List<LeaveBalance>> GetBalancesAsync(LeaveBalanceQuery query, DataScope scope);
    Task<bool> BalanceExistsAsync(int employeeId, int leaveTypeId, int year);
    Task AddBalanceAsync(LeaveBalance balance);

    // ---------- LeaveApproval ----------
    Task AddApprovalAsync(LeaveApproval approval);
    Task<(List<LeaveApproval> Items, int TotalCount)> GetApprovalsPagedAsync(DataScope scope, int page, int pageSize);

    // ---------- Dashboard ----------
    Task<Dictionary<LeaveRequestStatus, int>> CountByStatusAsync(DataScope scope);
    Task<int> CountEmployeesAsync(DataScope scope);
    Task<int> CountOnLeaveAsync(DataScope scope, DateTime day);

    Task SaveChangesAsync();
}
