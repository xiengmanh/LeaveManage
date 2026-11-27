namespace LeaveManage.DTOs.Common;

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? Detail { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>Chi tiết từng trường bị lỗi validation (ví dụ: { "email": ["Email không hợp lệ"] }).</summary>
    public Dictionary<string, List<string>>? Errors { get; set; }
}

