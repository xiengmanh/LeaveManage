using LeaveManage.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IHostEnvironment _environment;

    public SystemController(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>Trả về tên Environment hiện tại.</summary>
    [HttpGet("environment")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public IActionResult GetEnvironment()
    {
        return Ok(new { _environment.EnvironmentName });
    }
}
