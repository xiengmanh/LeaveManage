using LeaveManage.Constants;
using LeaveManage.Data;
using LeaveManage.DTOs.Common;
using LeaveManage.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<User>> GetAllAsync(
        UserQueryParameters queryParams)
    {
        var query = _db.Users
            .AsNoTracking()
            .AsQueryable();

        // 1. SEARCH (Tìm kiếm tương đối)
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.Trim().ToLower();
            query = query.Where(user =>
                user.Username.ToLower().Contains(term) ||
                user.Email.ToLower().Contains(term));
        }

        // 2. FILTER (Lọc chính xác)
        if (!string.IsNullOrWhiteSpace(queryParams.Role))
        {
            query = query.Where(user => user.Role == queryParams.Role);
        }

        // 3. SORTING
        query = queryParams.SortBy?.ToLower() switch
        {
            "email" => queryParams.IsDescending
                ? query.OrderByDescending(user => user.Email)
                : query.OrderBy(user => user.Email),

            "createdat" => queryParams.IsDescending
                ? query.OrderByDescending(user => user.CreatedAt)
                : query.OrderBy(user => user.CreatedAt),

            _ => queryParams.IsDescending
                ? query.OrderByDescending(user => user.Username)
                : query.OrderBy(user => user.Username)
        };

        // 4. COUNT (Tổng số dòng thỏa điều kiện search/filter)
        var totalCount = await query.CountAsync();

        // 5. PAGINATION (Chỉ lấy đúng PageSize dòng từ DB)
        var items = await query
            .Skip(PagingLimits.Skip(queryParams.PageNumber, queryParams.PageSize))
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<User>(
            items,
            totalCount,
            queryParams.PageNumber,
            queryParams.PageSize);
    }

    public Task<User?> GetByIdAsync(int id)
    {
        return _db.Users.FirstOrDefaultAsync(user => user.Id == id);
    }

    public Task<User?> GetByUsernameAsync(string username)
    {
        return _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Username == username);
    }

    public Task<User?> GetByUsernameOrEmailAsync(string username, string email)
    {
        return _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user =>
                user.Username == username || user.Email == email);
    }

    public Task<bool> IsUsernameOrEmailUsedByAnotherAsync(
        int currentUserId,
        string? username,
        string? email)
    {
        return _db.Users.AnyAsync(user =>
            user.Id != currentUserId &&
            ((!string.IsNullOrEmpty(username) && user.Username == username) ||
             (!string.IsNullOrEmpty(email) && user.Email == email)));
    }

    public Task AddAsync(User user)
    {
        return _db.Users.AddAsync(user).AsTask();
    }

    public void Update(User user)
    {
        _db.Users.Update(user);
    }

    public void Delete(User user)
    {
        _db.Users.Remove(user);
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _db.SaveChangesAsync() > 0;
    }

    public Task<User?> GetWithEmployeeAsync(int id) =>
        _db.Users.AsNoTracking().Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == id);

    public async Task UpdateEmployeePhoneAsync(int employeeId, string? phone) =>
        await _db.Employees.Where(e => e.Id == employeeId).ExecuteUpdateAsync(s => s
            .SetProperty(e => e.PhoneNumber, phone)
            .SetProperty(e => e.UpdatedAt, DateTime.UtcNow));

    public Task<bool> EmployeeExistsAsync(int employeeId) =>
        _db.Employees.AnyAsync(employee => employee.Id == employeeId);

    public Task<bool> IsEmployeeLinkedAsync(int employeeId, int? exceptUserId) =>
        _db.Users.AnyAsync(user => user.EmployeeId == employeeId
            && (exceptUserId == null || user.Id != exceptUserId));
}
