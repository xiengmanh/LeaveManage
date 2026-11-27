using LeaveManage.DTOs.Responses;

namespace LeaveManage.Authentication;

/// <summary>
/// Refresh token được gửi cho trình duyệt dưới dạng cookie HttpOnly (JavaScript không đọc được),
/// không nằm trong body JSON. Path giới hạn ở /api/users nên cookie chỉ gửi kèm các API đăng nhập/làm mới/đăng xuất.
/// SameSite=Strict chặn gửi cookie từ site khác (giảm CSRF với endpoint refresh).
/// Secure bật khi request đi qua HTTPS; HTTP thuần (dev nội bộ) vẫn hoạt động.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "lm_refresh";
    public const string Path = "/api/users";

    public static void Append(HttpResponse response, AuthResponseDto tokens) =>
        response.Cookies.Append(Name, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = new DateTimeOffset(tokens.RefreshTokenExpiresAt, TimeSpan.Zero)
        });

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = Path
        });
}
