using System.Text.Json.Serialization;

namespace LeaveManage.DTOs.Responses;

public class UserResponseDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public int? EmployeeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;

    // Refresh token chỉ đi qua cookie HttpOnly (xem RefreshTokenCookie), không trả trong body JSON.
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonIgnore]
    public DateTime RefreshTokenExpiresAt { get; set; }

    public DateTime ExpiresAt { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
}
