using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Exceptions;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public interface IActivityLogService
{
    /// <summary>Ghi một dòng nhật ký (không bao giờ ném lỗi ra ngoài để không làm hỏng nghiệp vụ chính).</summary>
    Task LogAsync(int? userId, string username, string action, string method, string path, int statusCode, string? ip);
    Task<PaginatedResponse<ActivityLogResponse>> GetAsync(ActivityLogQuery query, CallerContext caller, string callerUsername);
}

public class ActivityLogService : IActivityLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IActivityLogRepository _repository;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(IServiceScopeFactory scopeFactory, IActivityLogRepository repository, ILogger<ActivityLogService> logger)
    {
        _scopeFactory = scopeFactory;
        _repository = repository;
        _logger = logger;
    }

    public async Task LogAsync(int? userId, string username, string action, string method, string path, int statusCode, string? ip)
    {
        try
        {
            // Scope riêng (DbContext riêng): không vô tình lưu các thay đổi dở dang của request đang xử lý
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IActivityLogRepository>();
            await repo.AddAsync(new ActivityLog
            {
                UserId = userId,
                Username = Truncate(username, 50),
                Action = Truncate(action, 200),
                Method = Truncate(method, 10),
                Path = Truncate(path, 200),
                StatusCode = statusCode,
                IsSuccess = statusCode is >= 200 and < 400,
                IpAddress = Truncate(ip, 45),
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không ghi được nhật ký hoạt động ({Action})", action);
        }
    }

    public async Task<PaginatedResponse<ActivityLogResponse>> GetAsync(ActivityLogQuery query, CallerContext caller, string callerUsername)
    {
        if (query.Page < 1 || query.Page > PagingLimits.MaxPage)
            throw new BadRequestException($"Số trang phải từ 1 đến {PagingLimits.MaxPage}.", "INVALID_PAGE");
        query.PageSize = Math.Clamp(query.PageSize, 1, 50);

        // Admin xem tất cả; tài khoản khác chỉ xem của chính mình
        var (items, total) = await _repository.GetPagedAsync(query, caller.IsAdmin ? null : caller.UserId, caller.IsAdmin ? null : callerUsername);
        return new PaginatedResponse<ActivityLogResponse>
        {
            Items = items.Select(l => new ActivityLogResponse
            {
                Id = l.Id, Username = l.Username, Action = l.Action, Method = l.Method, Path = l.Path,
                StatusCode = l.StatusCode, IsSuccess = l.IsSuccess, IpAddress = l.IpAddress, CreatedAt = l.CreatedAt
            }).ToList(),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
