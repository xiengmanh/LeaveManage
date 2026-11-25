using System.Data;
using LeaveManage.Constants;
using LeaveManage.Data;
using LeaveManage.DTOs.Requests;
using LeaveManage.Models;
using LeaveManage.Services;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public class LeaveRepository : ILeaveRepository
{
    private readonly AppDbContext _context;

    public LeaveRepository(AppDbContext context)
    {
        _context = context;
    }

    // ===================== Transaction + khóa =====================

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var result = await work();
        await transaction.CommitAsync();
        return result;
        // Nếu work() ném lỗi: transaction chưa Commit sẽ tự Rollback khi dispose
    }

    public Task LockEmployeeAsync(int employeeId) =>
        // UPDLOCK giữ đến khi transaction kết thúc; request thứ hai cùng nhân viên phải chờ
        _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT Id FROM Employees WITH (UPDLOCK, ROWLOCK) WHERE Id = {employeeId}");

    // ===================== Employee / LeaveType =====================

    public Task<Employee?> GetEmployeeAsync(int employeeId) =>
        _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);

    public Task<LeaveType?> GetLeaveTypeAsync(int leaveTypeId) =>
        _context.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == leaveTypeId);

    // ===================== LeaveRequest =====================

    private IQueryable<LeaveRequest> ScopedRequests(DataScope scope)
    {
        var query = _context.LeaveRequests.AsNoTracking();
        if (!scope.AllEmployees)
        {
            query = query.Where(r => r.EmployeeId == scope.EmployeeId
                || (scope.IncludeDirectReports && r.Employee!.ManagerId == scope.EmployeeId));
        }
        return query;
    }

    public async Task<(List<LeaveRequest> Items, int TotalCount)> GetRequestsPagedAsync(
        LeaveRequestQuery q, DataScope scope, bool pendingForApprover = false)
    {
        IQueryable<LeaveRequest> query;
        if (pendingForApprover)
        {
            // Đơn chờ mà người gọi được duyệt: Admin = mọi đơn trừ đơn của chính mình; Manager = đơn của cấp dưới trực tiếp
            query = _context.LeaveRequests.AsNoTracking()
                .Where(r => r.Status == LeaveRequestStatus.Pending)
                .Where(r => scope.AllEmployees
                    ? r.EmployeeId != scope.EmployeeId
                    : r.Employee!.ManagerId == scope.EmployeeId);
        }
        else
        {
            query = ScopedRequests(scope);
        }

        if (q.Status.HasValue) query = query.Where(r => r.Status == q.Status);
        if (q.EmployeeId.HasValue) query = query.Where(r => r.EmployeeId == q.EmployeeId);
        if (q.LeaveTypeId.HasValue) query = query.Where(r => r.LeaveTypeId == q.LeaveTypeId);
        if (q.FromDate.HasValue) query = query.Where(r => r.EndDate >= q.FromDate.Value.Date);
        if (q.ToDate.HasValue) query = query.Where(r => r.StartDate <= q.ToDate.Value.Date);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(r => r.RequestNumber.Contains(term)
                || r.Employee!.FullName.Contains(term)
                || r.Employee!.EmployeeCode.Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip(PagingLimits.Skip(q.Page, q.PageSize))
            .Take(q.PageSize)
            .ToListAsync();
        return (items, total);
    }

    public Task<LeaveRequest?> GetRequestDetailAsync(int id) =>
        _context.LeaveRequests.AsNoTracking()
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .Include(r => r.Approvals).ThenInclude(a => a.Approver)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<int?> GetRequestOwnerIdAsync(int id) =>
        await _context.LeaveRequests.Where(r => r.Id == id)
            .Select(r => (int?)r.EmployeeId).FirstOrDefaultAsync();

    public Task<LeaveRequest?> GetRequestTrackedAsync(int id) =>
        _context.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == id);

    public Task<bool> HasOverlapAsync(int employeeId, DateTime start, DateTime end, int excludeRequestId) =>
        // Hai khoảng [s1,e1] và [s2,e2] giao nhau khi s1 <= e2 và s2 <= e1
        _context.LeaveRequests.AnyAsync(r => r.EmployeeId == employeeId
            && r.Id != excludeRequestId
            && (r.Status == LeaveRequestStatus.Pending || r.Status == LeaveRequestStatus.Approved)
            && r.StartDate <= end && start <= r.EndDate);

    public Task<int> SumDaysInYearAsync(int employeeId, int leaveTypeId, int year, int excludeRequestId) =>
        _context.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == leaveTypeId
                && r.Id != excludeRequestId && r.StartDate.Year == year
                && (r.Status == LeaveRequestStatus.Pending || r.Status == LeaveRequestStatus.Approved))
            .SumAsync(r => r.TotalDays);

    public Task AddRequestAsync(LeaveRequest request) =>
        _context.LeaveRequests.AddAsync(request).AsTask();

    // ===================== LeaveBalance =====================

    public Task<LeaveBalance?> GetBalanceTrackedAsync(int employeeId, int leaveTypeId, int year) =>
        _context.LeaveBalances.FirstOrDefaultAsync(b =>
            b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year);

    public Task<LeaveBalance?> GetBalanceTrackedByIdAsync(int id) =>
        _context.LeaveBalances
            .Include(b => b.Employee)
            .Include(b => b.LeaveType)
            .FirstOrDefaultAsync(b => b.Id == id);

    public Task<LeaveBalance?> GetBalanceByIdAsync(int id) =>
        _context.LeaveBalances.AsNoTracking()
            .Include(b => b.Employee)
            .Include(b => b.LeaveType)
            .FirstOrDefaultAsync(b => b.Id == id);

    public Task<List<LeaveBalance>> GetBalancesAsync(LeaveBalanceQuery q, DataScope scope)
    {
        var query = _context.LeaveBalances.AsNoTracking()
            .Include(b => b.Employee)
            .Include(b => b.LeaveType)
            .AsQueryable();
        if (!scope.AllEmployees)
        {
            query = query.Where(b => b.EmployeeId == scope.EmployeeId
                || (scope.IncludeDirectReports && b.Employee!.ManagerId == scope.EmployeeId));
        }
        if (q.Year.HasValue) query = query.Where(b => b.Year == q.Year);
        if (q.EmployeeId.HasValue) query = query.Where(b => b.EmployeeId == q.EmployeeId);
        if (q.LeaveTypeId.HasValue) query = query.Where(b => b.LeaveTypeId == q.LeaveTypeId);
        return query.OrderBy(b => b.EmployeeId).ThenBy(b => b.Year).ThenBy(b => b.LeaveTypeId).Take(500).ToListAsync();
    }

    public Task<bool> BalanceExistsAsync(int employeeId, int leaveTypeId, int year) =>
        _context.LeaveBalances.AnyAsync(b =>
            b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year);

    public Task AddBalanceAsync(LeaveBalance balance) =>
        _context.LeaveBalances.AddAsync(balance).AsTask();

    // ===================== LeaveApproval =====================

    public Task AddApprovalAsync(LeaveApproval approval) =>
        _context.LeaveApprovals.AddAsync(approval).AsTask();

    public async Task<(List<LeaveApproval> Items, int TotalCount)> GetApprovalsPagedAsync(
        DataScope scope, int page, int pageSize)
    {
        var query = _context.LeaveApprovals.AsNoTracking().AsQueryable();
        if (!scope.AllEmployees)
        {
            // Lịch sử liên quan đến mình: đơn của mình, đơn mình đã duyệt, đơn của cấp dưới (Manager)
            query = query.Where(a => a.ApproverId == scope.EmployeeId
                || a.LeaveRequest!.EmployeeId == scope.EmployeeId
                || (scope.IncludeDirectReports && a.LeaveRequest!.Employee!.ManagerId == scope.EmployeeId));
        }
        var total = await query.CountAsync();
        var items = await query
            .Include(a => a.Approver)
            .Include(a => a.LeaveRequest)
            .OrderByDescending(a => a.ActionDate).ThenByDescending(a => a.Id)
            .Skip(PagingLimits.Skip(page, pageSize)).Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    // ===================== Dashboard =====================

    public async Task<Dictionary<LeaveRequestStatus, int>> CountByStatusAsync(DataScope scope) =>
        await ScopedRequests(scope)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public Task<int> CountEmployeesAsync(DataScope scope)
    {
        var query = _context.Employees.Where(e => e.IsActive);
        if (!scope.AllEmployees)
            query = query.Where(e => e.Id == scope.EmployeeId
                || (scope.IncludeDirectReports && e.ManagerId == scope.EmployeeId));
        return query.CountAsync();
    }

    public Task<int> CountOnLeaveAsync(DataScope scope, DateTime day) =>
        ScopedRequests(scope)
            .Where(r => r.Status == LeaveRequestStatus.Approved && r.StartDate <= day && day <= r.EndDate)
            .Select(r => r.EmployeeId).Distinct().CountAsync();

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
