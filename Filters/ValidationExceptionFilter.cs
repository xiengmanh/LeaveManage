using LeaveManage.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LeaveManage.Filters;

/// <summary>
/// Xử lý validation errors từ [Required], [Range], [StringLength], etc.
/// Trả về 400 với chi tiết từng trường bị lỗi thay vì mô tả chung chung.
/// </summary>
public class ValidationExceptionFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(ms => ms.Value?.Errors.Count > 0)
                .ToDictionary(
                    ms => ms.Key,
                    ms => ms.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                            ? (e.Exception?.Message ?? "Lỗi không xác định")
                            : e.ErrorMessage)
                        .ToList());

            var response = new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Dữ liệu không hợp lệ.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = errors,
                TraceId = context.HttpContext.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            context.Result = new BadRequestObjectResult(response);
        }
    }
}
