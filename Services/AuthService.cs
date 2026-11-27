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

        // Mật khẩu đúng nhưng còn băm theo cơ chế cũ → băm lại theo chuẩn mới (lỗi ở bước này không chặn đăng nhập)
        if (_passwordService.IsLegacyHash(user.PasswordSalt))
        {
            _passwordService.CreateHash(request.Password, out var newHash, out var newSalt);
            await _authRepository.TryUpgradePasswordHashAsync(user.Id, user.PasswordHash, newHash, newSalt);
        }

        var (refreshToken, refreshHash, refreshExpiresAt) = CreateRefreshToken();
        // Chỉ lưu khi tài khoản vẫn hoạt động và chưa bị thu hồi phiên giữa chừng
        if (!await _authRepository.TryStoreRefreshTokenAsync(user.Id, user.TokenVersion, refreshHash, refreshExpiresAt))
            return null;

        return BuildResponse(user, refreshToken, refreshExpiresAt);
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var (newToken, newHash, newExpiresAt) = CreateRefreshToken();
        // Rotation nguyên tử ở Repository: chỉ một request thắng; token cũ không dùng lại được
        var user = await _authRepository.RotateRefreshTokenAsync(
            HashRefreshToken(refreshToken), newHash, newExpiresAt, DateTime.UtcNow);
        return user is null ? null : BuildResponse(user, newToken, newExpiresAt);
    }

    /// <summary>Đăng xuất: xóa refresh token và tăng TokenVersion → access JWT cũ cũng bị từ chối.</summary>
    public Task LogoutAsync(int userId) => _authRepository.RevokeSessionsAsync(userId);

    public async Task<bool> IsAccessTokenCurrentAsync(ClaimsPrincipal principal)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !int.TryParse(principal.FindFirstValue(CustomClaimTypes.TokenVersion), out var tokenVersion))
            return false;

        var state = await _authRepository.GetAuthStateAsync(userId);
        if (state is null || !state.IsActive || state.TokenVersion != tokenVersion)
            return false; // tài khoản bị xóa/khóa hoặc phiên đã bị thu hồi

        // Role và nhân viên trong token phải khớp dữ liệu hiện tại
        int? employeeIdInToken = int.TryParse(principal.FindFirstValue(CustomClaimTypes.EmployeeId), out var eid) ? eid : null;
        return principal.FindFirstValue(ClaimTypes.Role) == state.Role && employeeIdInToken == state.EmployeeId;
    }

    private (string Token, string Hash, DateTime ExpiresAt) CreateRefreshToken()
    {
        var refreshTokenDays = _configuration.GetSection("Jwt").GetValue<int>("RefreshTokenDays", 7);
        // Refresh token là chuỗi ngẫu nhiên 64 byte; database chỉ lưu bản băm SHA256
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (token, HashRefreshToken(token), DateTime.UtcNow.AddDays(refreshTokenDays));
    }

    private AuthResponseDto BuildResponse(User user, string refreshToken, DateTime refreshExpiresAt)
    {
        var expiresInMinutes = _configuration.GetSection("Jwt").GetValue<int>("ExpiresInMinutes", 60);
        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes);
        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user, accessTokenExpiresAt),
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshExpiresAt,
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
            new(CustomClaimTypes.TokenVersion, user.TokenVersion.ToString()),
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
