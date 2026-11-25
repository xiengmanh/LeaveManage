using System.Security.Cryptography;
using System.Text;

namespace LeaveManage.Services;

/// <summary>
/// Một cơ chế băm mật khẩu DUY NHẤT cho toàn hệ thống (đăng nhập, tạo tài khoản, seed):
/// HMACSHA512 với salt ngẫu nhiên riêng cho từng tài khoản (như bài lab Buổi 8).
/// </summary>
public interface IPasswordService
{
    void CreateHash(string password, out byte[] hash, out byte[] salt);
    bool Verify(string password, byte[] hash, byte[] salt);
}

public class PasswordService : IPasswordService
{
    public void CreateHash(string password, out byte[] hash, out byte[] salt)
    {
        using var hmac = new HMACSHA512();          // tạo key ngẫu nhiên 128 byte làm salt
        salt = hmac.Key;
        hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    public bool Verify(string password, byte[] hash, byte[] salt)
    {
        using var hmac = new HMACSHA512(salt);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        // So sánh thời gian cố định, tránh đoán mật khẩu qua thời gian phản hồi
        return computed.Length == hash.Length && CryptographicOperations.FixedTimeEquals(computed, hash);
    }
}
