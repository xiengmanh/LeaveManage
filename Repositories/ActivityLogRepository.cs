using LeaveManage.Data;
using LeaveManage.DTOs.Requests;
using LeaveManage.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public interface IActivityLogRepository
{
    Task AddAsync(ActivityLog log);
    /// <summary>userId/username = null → xem tất cả (Admin); ngược lại chỉ nhật ký của chính tài khoản đó.</summary>
    Task<(List<ActivityLog> Items, int TotalCount)> GetPagedAsync(ActivityLogQuery query, int? userId, string? username);
}

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly AppDbContext _db;

    public ActivityLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ActivityLog log)
    {
        _db.ActivityLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<(List<ActivityLog> Items, int TotalCount)> GetPagedAsync(ActivityLogQuery query, int? userId, string? username)
    {
        var q = _db.ActivityLogs.AsNoTracking().AsQueryable();
        if (userId is int uid)
        {
            // Nhật ký của mình, kể cả lần đăng nhập thất bại vào đúng tên đăng nhập của mình
            q = q.Where(l => l.UserId == uid || (l.UserId == null && l.Username == username));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(l => l.Username.Contains(term) || l.Action.Contains(term));
        }
        if (query.Success is bool ok) q = q.Where(l => l.IsSuccess == ok);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        return (items, total);
    }
}
