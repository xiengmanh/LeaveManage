using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;

namespace LeaveManage.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(UserLoginDto request);
    Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(int userId);
}
