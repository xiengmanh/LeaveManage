namespace LeaveManage.Services;

/// <summary>
/// Chạy định kỳ để đánh dấu đơn quá hạn là Expired. Lỗi trong một vòng chỉ được ghi log,
/// không làm dừng tiến trình nền.
/// </summary>
public class LeaveExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaveExpiryBackgroundService> _logger;

    public LeaveExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LeaveExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ILeaveExpiryService>();
                var count = await service.ExpireOverdueAsync(DateTime.Today);
                if (count > 0)
                    _logger.LogInformation("Expired {Count} overdue leave request(s).", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi xử lý đơn nghỉ quá hạn; sẽ thử lại sau {Interval}.", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
