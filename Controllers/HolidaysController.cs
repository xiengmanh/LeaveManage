using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using LeaveManage.Models;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

/// <summary>Lịch nghỉ lễ. Mọi người đăng nhập đều xem được; chỉ Admin thêm/xóa.</summary>
[ApiController]
[Route("api/holidays")]
[Produces("application/json")]
[Authorize]
public class HolidaysController : ControllerBase
{
    private readonly IHolidayService _service;
    public HolidaysController(IHolidayService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<Holiday>>> GetAll([FromQuery] int? year) =>
        Ok(await _service.GetAllAsync(year));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<Holiday>> Create([FromBody] HolidayUpsertDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), new { year = created.Date.Year }, created);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
