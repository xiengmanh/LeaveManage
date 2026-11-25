using System.Security.Claims;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

/// <summary>Nhật ký hoạt động. Admin xem của tất cả tài khoản; tài khoản khác chỉ xem của chính mình.</summary>
[ApiController]
[Route("api/activity-logs")]
[Authorize]
public class ActivityLogsController : ControllerBase
{
    private readonly IActivityLogService _service;

    public ActivityLogsController(IActivityLogService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ActivityLogResponse>>> Get([FromQuery] ActivityLogQuery query) =>
        Ok(await _service.GetAsync(query, CallerContext.From(User), User.FindFirstValue(ClaimTypes.Name) ?? string.Empty));
}
