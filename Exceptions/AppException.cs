namespace LeaveManage.Exceptions;

/// <summary>
/// Lỗi nghiệp vụ có chủ đích. Middleware chuyển thành JSON ErrorResponse với đúng HTTP status và mã lỗi.
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }

    public AppException(
        string message,
        int statusCode = StatusCodes.Status400BadRequest,
        string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message, string? errorCode = "NOT_FOUND")
        : base(message, StatusCodes.Status404NotFound, errorCode) { }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message, string? errorCode = "BAD_REQUEST")
        : base(message, StatusCodes.Status400BadRequest, errorCode) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message, string? errorCode = "UNAUTHORIZED")
        : base(message, StatusCodes.Status401Unauthorized, errorCode) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message, string? errorCode = "FORBIDDEN")
        : base(message, StatusCodes.Status403Forbidden, errorCode) { }
}

/// <summary>Xung đột trạng thái/dữ liệu: trùng, chồng lấn ngày, không đủ phép, đã xử lý...</summary>
public class ConflictException : AppException
{
    public ConflictException(string message, string? errorCode = "CONFLICT")
        : base(message, StatusCodes.Status409Conflict, errorCode) { }
}
