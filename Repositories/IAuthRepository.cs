using LeaveManage.Models;

namespace LeaveManage.Repositories;

/// <summary>Trạng thái tài khoản hiện tại, dùng để kiểm tra JWT ở mỗi request.</summary>
public sealed record AccountAuthState(bool IsActive, int TokenVersion, string Role, int? EmployeeId);

public interface IAuthRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<AccountAuthState?> GetAuthStateAsync(int userId);

    /// <summary>Lưu refresh token khi đăng nhập, chỉ thành công nếu tài khoản còn hoạt động và TokenVersion chưa đổi.</summary>
    Task<bool> TryStoreRefreshTokenAsync(int userId, int expectedTokenVersion, string refreshTokenHash, DateTime expiresAt);

    /// <summary>
    /// Đổi refresh token NGUYÊN TỬ: UPDATE có điều kiện (hash cũ + còn hạn + tài khoản hoạt động).
    /// Nhiều request cùng token thì chỉ một request cập nhật được dòng; các request khác nhận null.
    /// </summary>
    Task<User?> RotateRefreshTokenAsync(string oldHash, string newHash, DateTime newExpiresAt, DateTime now);

    /// <summary>Thu hồi mọi phiên: xóa refresh token và tăng TokenVersion (JWT cũ bị từ chối).</summary>
    Task RevokeSessionsAsync(int userId);
}
