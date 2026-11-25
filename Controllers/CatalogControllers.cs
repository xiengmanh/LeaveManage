using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

/// <summary>Danh mục phòng ban. Mọi người đăng nhập được xem; chỉ Admin thêm/sửa/ngừng dùng.</summary>
[ApiController]
[Route("api/departments")]
[Produces("application/json")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly ICatalogService _service;
    public DepartmentsController(ICatalogService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<DepartmentResponse>>> GetAll([FromQuery] bool includeInactive = false) =>
        Ok(await _service.GetDepartmentsAsync(includeInactive && User.IsInRole(UserRoles.Admin)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DepartmentResponse>> GetById(int id) => Ok(await _service.GetDepartmentAsync(id));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<DepartmentResponse>> Create([FromBody] DepartmentUpsertDto dto)
    {
        var created = await _service.CreateDepartmentAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<DepartmentResponse>> Update(int id, [FromBody] DepartmentUpsertDto dto) =>
        Ok(await _service.UpdateDepartmentAsync(id, dto));

    /// <summary>Ngừng sử dụng (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeactivateDepartmentAsync(id);
        return NoContent();
    }
}

/// <summary>Danh mục chức vụ.</summary>
[ApiController]
[Route("api/positions")]
[Produces("application/json")]
[Authorize]
public class PositionsController : ControllerBase
{
    private readonly ICatalogService _service;
    public PositionsController(ICatalogService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<PositionResponse>>> GetAll([FromQuery] bool includeInactive = false) =>
        Ok(await _service.GetPositionsAsync(includeInactive && User.IsInRole(UserRoles.Admin)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PositionResponse>> GetById(int id) => Ok(await _service.GetPositionAsync(id));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<PositionResponse>> Create([FromBody] PositionUpsertDto dto)
    {
        var created = await _service.CreatePositionAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<PositionResponse>> Update(int id, [FromBody] PositionUpsertDto dto) =>
        Ok(await _service.UpdatePositionAsync(id, dto));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeactivatePositionAsync(id);
        return NoContent();
    }
}

/// <summary>Danh mục loại nghỉ.</summary>
[ApiController]
[Route("api/leave-types")]
[Produces("application/json")]
[Authorize]
public class LeaveTypesController : ControllerBase
{
    private readonly ICatalogService _service;
    public LeaveTypesController(ICatalogService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<LeaveTypeResponse>>> GetAll([FromQuery] bool includeInactive = false) =>
        Ok(await _service.GetLeaveTypesAsync(includeInactive && User.IsInRole(UserRoles.Admin)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeaveTypeResponse>> GetById(int id) => Ok(await _service.GetLeaveTypeAsync(id));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<LeaveTypeResponse>> Create([FromBody] LeaveTypeUpsertDto dto)
    {
        var created = await _service.CreateLeaveTypeAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<LeaveTypeResponse>> Update(int id, [FromBody] LeaveTypeUpsertDto dto) =>
        Ok(await _service.UpdateLeaveTypeAsync(id, dto));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeactivateLeaveTypeAsync(id);
        return NoContent();
    }
}
