using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public interface IDashboardService
{
    Task<DashboardResponse> GetAsync(CallerContext caller);
}

/// <summary>Số liệu tổng quan, luôn tính trong phạm vi dữ liệu của người gọi.</summary>
public class DashboardService : IDashboardService
{
    private readonly ILeaveRepository _repository;

    public DashboardService(ILeaveRepository repository)
    {
        _repository = repository;
    }

    public async Task<DashboardResponse> GetAsync(CallerContext caller)
    {
        var scope = DataScope.For(caller);
        var counts = await _repository.CountByStatusAsync(scope);
        int Count(LeaveRequestStatus status) => counts.TryGetValue(status, out var n) ? n : 0;

        var awaiting = 0;
        if ((caller.IsAdmin || caller.IsManager) && caller.EmployeeId.HasValue)
        {
            var approverScope = new DataScope(caller.IsAdmin, caller.EmployeeId.Value, caller.IsManager);
            (_, awaiting) = await _repository.GetRequestsPagedAsync(
                new LeaveRequestQuery { Page = 1, PageSize = 1 }, approverScope, pendingForApprover: true);
        }

        var myBalances = new List<LeaveBalanceResponse>();
        if (caller.EmployeeId is int me)
        {
            var balances = await _repository.GetBalancesAsync(
                new LeaveBalanceQuery { EmployeeId = me, Year = DateTime.Today.Year },
                new DataScope(false, me, false));
            myBalances = balances.Select(b => new LeaveBalanceResponse
            {
                Id = b.Id,
                EmployeeId = b.EmployeeId,
                LeaveTypeId = b.LeaveTypeId,
                LeaveTypeName = b.LeaveType?.Name,
                Year = b.Year,
                InitialDays = b.InitialDays,
                UsedDays = b.UsedDays,
                PendingDays = b.PendingDays,
                RemainingDays = b.RemainingDays
            }).ToList();
        }

        return new DashboardResponse
        {
            Scope = caller.IsAdmin ? "Toàn công ty" : caller.IsManager ? "Bản thân và nhân viên cấp dưới" : "Bản thân",
            EmployeeCount = await _repository.CountEmployeesAsync(scope),
            DraftCount = Count(LeaveRequestStatus.Draft),
            PendingCount = Count(LeaveRequestStatus.Pending),
            ApprovedCount = Count(LeaveRequestStatus.Approved),
            RejectedCount = Count(LeaveRequestStatus.Rejected),
            CancelledCount = Count(LeaveRequestStatus.Cancelled),
            AwaitingMyApproval = awaiting,
            OnLeaveToday = await _repository.CountOnLeaveAsync(scope, DateTime.Today),
            MyBalances = myBalances
        };
    }
}
