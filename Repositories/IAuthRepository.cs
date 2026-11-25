using LeaveManage.Models;

namespace LeaveManage.Repositories;

public interface IAuthRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash);
    Task<bool> SaveChangesAsync();
}
