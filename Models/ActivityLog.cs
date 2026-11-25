using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveManage.Models;

/// <summary>Nhật ký hoạt động: ai làm gì, lúc nào, kết quả ra sao. Không lưu nội dung request (mật khẩu...).</summary>
public class ActivityLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>Null khi đăng nhập thất bại (chưa xác định được tài khoản).</summary>
    public int? UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Method { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Path { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public bool IsSuccess { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
