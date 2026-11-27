using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

/// <summary>
/// Chuyển các đơn còn Pending mà ngày bắt đầu đã qua sang Expired (không ai kịp duyệt).
/// Số ngày đang giữ chỗ (PendingDays) được trả lại như khi hủy đơn.
/// </summary>
public interface ILeaveExpiryService
{
    /// <summary>Trả về số đơn đã chuyển sang Expired. Đơn có ngày bắt đầu nhỏ hơn <paramref name="today"/> là quá hạn.</summary>
    Task<int> ExpireOverdueAsync(DateTime today);
}

public class LeaveExpiryService : ILeaveExpiryService
{
    private readonly ILeaveRepository _repository;
    private readonly ILogger<LeaveExpiryService> _logger;

    public LeaveExpiryService(ILeaveRepository repository, ILogger<LeaveExpiryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> ExpireOverdueAsync(DateTime today)
    {
        var cutoff = today.Date;
        var candidates = await _repository.GetOverduePendingRequestKeysAsync(cutoff);
        var expired = 0;

        foreach (var (requestId, employeeId) in candidates)
        {
            // Mỗi đơn một transaction, khóa nhân viên như các thao tác khác để không đua với duyệt/hủy.
            // Đọc lại trạng thái sau khi có khóa: đơn có thể vừa được duyệt hoặc hủy.
            var changed = await _repository.ExecuteInTransactionAsync(async () =>
            {
                await _repository.LockEmployeeAsync(employeeId);

                var request = await _repository.GetRequestTrackedAsync(requestId);
                if (request is null
                    || request.Status != LeaveRequestStatus.Pending
                    || request.StartDate.Date >= cutoff)
                    return false;

                if (request.LeaveType!.AllowsDeduction)
                {
                    var balance = await _repository.GetBalanceTrackedAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
                    if (balance is not null)
                    {
                        balance.PendingDays -= request.TotalDays;
                        balance.UpdatedAt = DateTime.UtcNow;
                    }
                }

                request.Status = LeaveRequestStatus.Expired;
                request.UpdatedAt = DateTime.UtcNow;
                await _repository.SaveChangesAsync();
                return true;
            });

            if (changed)
            {
                expired++;
                _logger.LogInformation("Leave request {RequestId} expired (start date passed without a decision)", requestId);
            }
        }

        return expired;
    }
}
