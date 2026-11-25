using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ValidationController : ControllerBase
{
    /// <summary>Minh họa Custom Validation Attribute MustBeEven.</summary>
    [HttpPost("even-number")]
    public IActionResult CheckEvenNumber([FromBody] EvenNumberRequest request)
    {
        return Ok(new { Message = $"{request.Value} là số chẵn hợp lệ." });
    }
}
