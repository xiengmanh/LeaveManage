using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveManage.Models;

/// <summary>
/// Tài khoản đăng nhập của hệ thống.
/// </summary>
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public byte[] PasswordHash { get; set; } = [];

    [Required]
    public byte[] PasswordSalt { get; set; } = [];

    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = "User";

    [Required]
    [MaxLength(50)]
    public string Department { get; set; } = "General";

    [Range(18, 100)]
    public int Age { get; set; } = 18;

    public bool IsActive { get; set; } = true;

    /// <summary>Nhân viên sở hữu tài khoản này (null nếu không gắn nhân viên). Quan hệ 1 - 0..1.</summary>
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [MaxLength(128)]
    public string? RefreshTokenHash { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Phiên bản phiên đăng nhập. Ghi vào JWT (claim TokenVersion) và được kiểm tra ở MỖI request.
    /// Tăng lên khi logout, khóa tài khoản, đổi role/nhân viên/mật khẩu → mọi JWT cũ hết hiệu lực ngay.
    /// </summary>
    public int TokenVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
