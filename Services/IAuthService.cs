using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;

namespace LeaveManage.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(UserLoginDto request);
    Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(int userId);

    /// <summary>JWT còn khớp với trạng thái tài khoản hiện tại không (gọi ở mỗi request).</summary>
    Task<bool> IsAccessTokenCurrentAsync(System.Security.Claims.ClaimsPrincipal principal);
}
