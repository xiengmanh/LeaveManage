namespace LeaveManage.DTOs.Responses;

public class ProfileResponseDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
}

public class ActivityLogResponse
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
