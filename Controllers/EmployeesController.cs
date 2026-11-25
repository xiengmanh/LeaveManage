using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

/// <summary>API quản lý nhân viên.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>Danh sách nhân viên (phân trang, tìm kiếm, lọc). Admin: tất cả; Manager: mình + cấp dưới trực tiếp; User: chính mình.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<EmployeeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<EmployeeResponse>>> GetAll(
        [FromQuery] EmployeeQueryRequest query)
    {
        return Ok(await _employeeService.GetAllAsync(query, CallerContext.From(User)));
    }

    /// <summary>Lấy thông tin nhân viên theo ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id, CallerContext.From(User));
        return employee is null
            ? NotFound(ErrorResponse.Fail("Không tìm thấy nhân viên", "NOT_FOUND"))
            : Ok(employee);
    }

    /// <summary>Lấy thông tin đầy đủ của nhân viên.</summary>
    [HttpGet("{id:int}/full")]
    public async Task<ActionResult<EmployeeResponse>> GetByIdFull(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id, CallerContext.From(User), includeLeaveBalances: true);
        return employee is null
            ? NotFound(ErrorResponse.Fail("Không tìm thấy nhân viên", "NOT_FOUND"))
            : Ok(employee);
    }

    /// <summary>Tạo nhân viên mới. Chỉ Admin được phép thực hiện.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeResponse>> Create(
        [FromBody] CreateEmployeeRequest request)
    {
        var result = await _employeeService.CreateAsync(request);
        if (!result.Success)
            return result.ErrorCode is "DUPLICATE_CODE" or "DUPLICATE_EMAIL"
                ? Conflict(ErrorResponse.Fail(result.Message, result.ErrorCode))
                : BadRequest(ErrorResponse.Fail(result.Message, result.ErrorCode));

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Data!.Id },
            result.Data);
    }

    /// <summary>Cập nhật nhân viên. Chỉ Admin (Manager không được tự đổi phòng ban/quản lý/trạng thái).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeResponse>> Update(
        int id,
        [FromBody] UpdateEmployeeRequest request)
    {
        var result = await _employeeService.UpdateAsync(id, request);
        if (!result.Success)
        {
            var error = ErrorResponse.Fail(result.Message, result.ErrorCode);
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(error),
                "DUPLICATE_EMAIL" => Conflict(error),
                _ => BadRequest(error)
            };
        }

        return Ok(result.Data);
    }

    /// <summary>Vô hiệu hóa nhân viên. Chỉ Admin được phép thực hiện.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<SuccessResponse>> Delete(int id)
    {
        var result = await _employeeService.DeleteAsync(id);
        return result.Success
            ? Ok(SuccessResponse.Ok(result.Message))
            : NotFound(ErrorResponse.Fail(result.Message, result.ErrorCode));
    }

    /// <summary>Lấy danh sách phòng ban đang hoạt động.</summary>
    [HttpGet("departments")]
    public async Task<ActionResult<List<DepartmentResponse>>> GetDepartments()
    {
        return Ok(await _employeeService.GetDepartmentsAsync());
    }

    /// <summary>Lấy một phòng ban và danh sách nhân viên thuộc phòng ban đó.</summary>
    [HttpGet("departments/{id:int}/employees")]
    [Authorize(Policy = AuthorizationPolicies.MustBeHR)]
    public async Task<ActionResult<DepartmentEmployeesResponse>> GetDepartmentEmployees(int id)
    {
        var result = await _employeeService.GetDepartmentEmployeesAsync(id);
        return result is null
            ? NotFound(ErrorResponse.Fail("Không tìm thấy phòng ban", "DEPARTMENT_NOT_FOUND"))
            : Ok(result);
    }

    /// <summary>Lấy danh sách chức vụ đang hoạt động.</summary>
    [HttpGet("positions")]
    public async Task<ActionResult<List<PositionResponse>>> GetPositions()
    {
        return Ok(await _employeeService.GetPositionsAsync());
    }
}
