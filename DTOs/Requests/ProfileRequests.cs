using System.ComponentModel.DataAnnotations;

namespace LeaveManage.DTOs.Requests;

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới dài 8-100 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UpdatePhoneDto
{
    /// <summary>Số điện thoại Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx). Để trống để xóa số.</summary>
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

public class ActivityLogQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    /// <summary>Tìm theo tên đăng nhập hoặc nội dung hành động.</summary>
    public string? Search { get; set; }
    /// <summary>true = thành công, false = thất bại, bỏ trống = tất cả.</summary>
    public bool? Success { get; set; }
}
