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
        _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Username == username);

    public Task<AccountAuthState?> GetAuthStateAsync(int userId) =>
        _context.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AccountAuthState(user.IsActive, user.TokenVersion, user.Role, user.EmployeeId))
            .FirstOrDefaultAsync();

    public async Task<bool> TryStoreRefreshTokenAsync(int userId, int expectedTokenVersion, string refreshTokenHash, DateTime expiresAt)
    {
        var rows = await _context.Users
            .Where(user => user.Id == userId && user.IsActive && user.TokenVersion == expectedTokenVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.RefreshTokenHash, refreshTokenHash)
                .SetProperty(user => user.RefreshTokenExpiresAt, expiresAt));
        return rows == 1;
    }

    public async Task<User?> RotateRefreshTokenAsync(string oldHash, string newHash, DateTime newExpiresAt, DateTime now)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Một câu UPDATE có điều kiện: SQL Server khóa dòng khi cập nhật, request đến sau phải chờ
        // rồi đánh giá lại điều kiện trên dữ liệu mới → hash cũ không còn khớp → 0 dòng → thua.
        var rows = await _context.Users
            .Where(user => user.RefreshTokenHash == oldHash
                && user.RefreshTokenExpiresAt > now
                && user.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.RefreshTokenHash, newHash)
                .SetProperty(user => user.RefreshTokenExpiresAt, newExpiresAt));
        if (rows != 1)
            return null; // không commit → rollback khi dispose

        // Đọc thông tin (gồm TokenVersion) trong cùng transaction, trước khi logout/khóa kịp chen vào
        var user = await _context.Users.AsNoTracking().FirstAsync(u => u.RefreshTokenHash == newHash);
        await transaction.CommitAsync();
        return user;
    }

    public async Task<bool> TryUpgradePasswordHashAsync(int userId, byte[] oldHash, byte[] newHash, byte[] newSalt)
    {
        var rows = await _context.Users
            .Where(user => user.Id == userId && user.PasswordHash == oldHash)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.PasswordHash, newHash)
                .SetProperty(user => user.PasswordSalt, newSalt));
        return rows == 1;
    }

    public Task RevokeSessionsAsync(int userId) =>
        _context.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.TokenVersion, user => user.TokenVersion + 1)
                .SetProperty(user => user.RefreshTokenHash, (string?)null)
                .SetProperty(user => user.RefreshTokenExpiresAt, (DateTime?)null));
}
