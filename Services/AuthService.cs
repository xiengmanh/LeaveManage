using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Models;
using LeaveManage.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace LeaveManage.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IConfiguration _configuration;
    private readonly IPasswordService _passwordService;

    public AuthService(IAuthRepository authRepository, IConfiguration configuration, IPasswordService passwordService)
    {
        _authRepository = authRepository;
        _configuration = configuration;
        _passwordService = passwordService;
    }

    public async Task<AuthResponseDto?> LoginAsync(UserLoginDto request)
    {
        var user = await _authRepository.GetByUsernameAsync(request.Username.Trim());
        // Sai username, sai mật khẩu hay tài khoản bị khóa đều trả null → cùng một thông báo 401
        if (user is null || !user.IsActive || !_passwordService.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
            return null;

        return await IssueTokenPairAsync(user);
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        var user = await _authRepository.GetByRefreshTokenHashAsync(tokenHash);

        if (user is null || !user.IsActive || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
            return null;

        // Rotation: refresh token cũ bị thay bằng token mới, nên dùng lại token cũ sẽ thất bại
        return await IssueTokenPairAsync(user);
    }

    public async Task LogoutAsync(int userId)
    {
        // Thu hồi refresh token: sau khi đăng xuất không thể xin access token mới
        var user = await _authRepository.GetByIdAsync(userId);
        if (user is null) return;
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        await _authRepository.SaveChangesAsync();
    }

    private async Task<AuthResponseDto> IssueTokenPairAsync(User user)
    {
        var settings = _configuration.GetSection("Jwt");
        var expiresInMinutes = settings.GetValue<int>("ExpiresInMinutes", 60);
        var refreshTokenDays = settings.GetValue<int>("RefreshTokenDays", 7);
        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes);

        // Refresh token là chuỗi ngẫu nhiên 64 byte; database chỉ lưu bản băm SHA256
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshTokenHash = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays);
        await _authRepository.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user, accessTokenExpiresAt),
            RefreshToken = refreshToken,
            ExpiresAt = accessTokenExpiresAt,
            Username = user.Username,
            Role = user.Role,
            EmployeeId = user.EmployeeId
        };
    }

    private string GenerateJwtToken(User user, DateTime expiresAt)
    {
        var settings = _configuration.GetSection("Jwt");
        var keyText = settings["Key"]
            ?? throw new InvalidOperationException("Missing configuration: Jwt:Key (đặt bằng dotnet user-secrets)");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new(CustomClaimTypes.Department, user.Department),
            new(CustomClaimTypes.Age, user.Age.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (user.EmployeeId is int employeeId)
            claims.Add(new Claim(CustomClaimTypes.EmployeeId, employeeId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: settings["Issuer"],
            audience: settings["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
