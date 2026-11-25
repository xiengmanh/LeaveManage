using System.ComponentModel.DataAnnotations;

namespace LeaveManage.DTOs.Requests;

public class UserRegisterDto
{
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Range(18, 60)]
    public int Age { get; set; } = 18;

    /// <summary>Admin, Manager hoặc User (mặc định User).</summary>
    [StringLength(30)]
    public string? Role { get; set; }

    /// <summary>Nhân viên gắn với tài khoản (1 nhân viên tối đa 1 tài khoản).</summary>
    [Range(1, int.MaxValue)]
    public int? EmployeeId { get; set; }
}

public class UserLoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class UserUpdateDto
{
    [StringLength(50)]
    public string? Username { get; set; }

    [EmailAddress]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }

    [StringLength(30)]
    public string? Role { get; set; }

    [StringLength(50)]
    public string? Department { get; set; }

    [Range(18, 100)]
    public int? Age { get; set; }

    /// <summary>false = khóa tài khoản (không đăng nhập/refresh được).</summary>
    public bool? IsActive { get; set; }

    /// <summary>Gắn tài khoản với nhân viên khác.</summary>
    [Range(1, int.MaxValue)]
    public int? EmployeeId { get; set; }
}
