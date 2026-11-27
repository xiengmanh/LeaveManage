using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace LeaveManage.Services;

/// <summary>
/// Cơ chế băm mật khẩu cho toàn hệ thống (đăng nhập, tạo tài khoản, seed, đổi mật khẩu).
/// Mật khẩu mới dùng PasswordHasher của ASP.NET Core Identity (PBKDF2-HMACSHA256, lặp 100.000 vòng,
/// salt ngẫu nhiên 128 bit, định dạng tự mang tham số). Chuỗi băm được lưu vào cột PasswordHash dưới dạng UTF-8,
/// còn PasswordSalt để mảng rỗng làm dấu hiệu "định dạng mới".
///
/// Tài khoản tạo theo cơ chế cũ (HMACSHA512, PasswordSalt khác rỗng) vẫn đăng nhập được.
/// Khi đăng nhập thành công, AuthService băm lại bằng cơ chế mới (xem IsLegacyHash).
/// </summary>
public interface IPasswordService
{
    void CreateHash(string password, out byte[] hash, out byte[] salt);
    bool Verify(string password, byte[] hash, byte[] salt);

    /// <summary>true nếu bản băm được tạo bằng cơ chế cũ và nên băm lại sau khi đăng nhập đúng.</summary>
    bool IsLegacyHash(byte[] salt);
}

public class PasswordService : IPasswordService
{
    // PasswordHasher không dùng tham số user khi băm/xác minh; chuỗi rỗng chỉ để đáp ứng chữ ký generic.
    private static readonly PasswordHasher<string> Hasher = new();

    public void CreateHash(string password, out byte[] hash, out byte[] salt)
    {
        hash = Encoding.UTF8.GetBytes(Hasher.HashPassword(string.Empty, password));
        salt = [];
    }

    public bool Verify(string password, byte[] hash, byte[] salt)
    {
        if (!IsLegacyHash(salt))
        {
            var stored = Encoding.UTF8.GetString(hash);
            var result = Hasher.VerifyHashedPassword(string.Empty, stored, password);
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }

        // Định dạng cũ: HMACSHA512 với salt là key ngẫu nhiên 128 byte (như bài lab Buổi 8)
        using var hmac = new HMACSHA512(salt);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return computed.Length == hash.Length && CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    public bool IsLegacyHash(byte[] salt) => salt.Length > 0;
}
