using LeaveManage.DTOs.Common;
using LeaveManage.Models;

namespace LeaveManage.Repositories;

public interface IUserRepository
{
    Task<PagedResult<User>> GetAllAsync(UserQueryParameters queryParams);
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByUsernameOrEmailAsync(string username, string email);
    Task<bool> IsUsernameOrEmailUsedByAnotherAsync(
        int currentUserId,
        string? username,
        string? email);
    Task<bool> EmployeeExistsAsync(int employeeId);
    /// <summary>Nhân viên đã gắn với tài khoản khác chưa (bỏ qua exceptUserId).</summary>
    Task<bool> IsEmployeeLinkedAsync(int employeeId, int? exceptUserId);
    Task<User?> GetWithEmployeeAsync(int id);
    Task UpdateEmployeePhoneAsync(int employeeId, string? phone);
    Task AddAsync(User user);
    void Update(User user);
    void Delete(User user);
    Task<bool> SaveChangesAsync();
}
