using LeaveManage.Data;
using LeaveManage.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByUsernameAsync(string username) =>
        _context.Users.FirstOrDefaultAsync(user => user.Username == username);

    public Task<User?> GetByIdAsync(int id) =>
        _context.Users.FirstOrDefaultAsync(user => user.Id == id);

    public Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash) =>
        _context.Users.FirstOrDefaultAsync(user => user.RefreshTokenHash == refreshTokenHash);

    public async Task<bool> SaveChangesAsync() => await _context.SaveChangesAsync() > 0;
}
