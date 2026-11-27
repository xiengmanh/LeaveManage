using System.Text.Json;
using LeaveManage.DTOs.Common;
using LeaveManage.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var (statusCode, message, errorCode, errors) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception. TraceId={TraceId} {Method} {Path}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest)
        {
            _logger.LogWarning(
                exception,
                "Client error {StatusCode}. TraceId={TraceId} {Method} {Path}",
                statusCode,
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path);
        }

        var response = new ErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors,
            Detail = _environment.IsDevelopment() && statusCode >= StatusCodes.Status500InternalServerError
                ? exception.ToString()
                : null,
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions));
    }

    private static (int StatusCode, string Message, string? ErrorCode, Dictionary<string, List<string>>? Errors) MapException(
        Exception exception)
    {
        return exception switch
        {
            AppException appException =>
                (appException.StatusCode, appException.Message, appException.ErrorCode, null),
            BadHttpRequestException badReq =>
                (StatusCodes.Status400BadRequest, badReq.Message, "BAD_REQUEST", null),
            // Validation errors from model binding (e.g., [Required], [Range], [StringLength])
            InvalidOperationException { Message: var msg } when msg.Contains("validation", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, msg, "VALIDATION_ERROR", null),
            // Hai request cùng sửa một dòng (rowversion) → 409
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Dữ liệu đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.", "CONCURRENCY_CONFLICT", null),
            // Vi phạm unique index (2601/2627) → 409
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                (StatusCodes.Status409Conflict, "Dữ liệu bị trùng với bản ghi đã có.", "DUPLICATE", null),
            // Vi phạm khóa ngoại/check constraint (547) → 409
            DbUpdateException { InnerException: SqlException { Number: 547 } } =>
                (StatusCodes.Status409Conflict, "Dữ liệu đang được tham chiếu hoặc vi phạm ràng buộc.", "CONSTRAINT_VIOLATION", null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
                "INTERNAL_ERROR",
                null)
        };
    }
}
