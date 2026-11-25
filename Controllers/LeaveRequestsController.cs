using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

/// <summary>
/// Đơn nghỉ phép. Controller chỉ nhận request, lấy thông tin người gọi từ JWT
/// rồi giao cho Service; mọi kiểm tra quyền sở hữu và nghiệp vụ nằm ở Service.
/// Lỗi nghiệp vụ (400/403/404/409) do ExceptionHandlingMiddleware trả về.
/// </summary>
[ApiController]
[Route("api/leave-requests")]
[Produces("application/json")]
[Authorize]
public class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _service;

    public LeaveRequestsController(ILeaveRequestService service)
    {
        _service = service;
    }

    private CallerContext Caller => CallerContext.From(User);

    /// <summary>Danh sách đơn trong phạm vi được xem (Admin: tất cả; Manager: mình + cấp dưới; User: của mình).</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<LeaveRequestResponse>>> GetList([FromQuery] LeaveRequestQuery query) =>
        Ok(await _service.GetListAsync(query, Caller));

    /// <summary>Đơn đang chờ mà người gọi có quyền duyệt.</summary>
    [HttpGet("pending-approval")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveLeave)]
    public async Task<ActionResult<PaginatedResponse<LeaveRequestResponse>>> GetPendingForApproval([FromQuery] LeaveRequestQuery query) =>
        Ok(await _service.GetPendingForApprovalAsync(query, Caller));

    /// <summary>Chi tiết đơn (kèm lịch sử duyệt). Ngoài phạm vi → 404.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeaveRequestResponse>> GetById(int id) =>
        Ok(await _service.GetByIdAsync(id, Caller));

    /// <summary>Tạo đơn nháp (Draft) cho chính mình.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.HasEmployee)]
    [ProducesResponseType(typeof(LeaveRequestResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<LeaveRequestResponse>> Create([FromBody] CreateLeaveRequestDto dto)
    {
        var created = await _service.CreateAsync(dto, Caller);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Sửa đơn khi còn là nháp (chỉ chủ đơn).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.HasEmployee)]
    public async Task<ActionResult<LeaveRequestResponse>> UpdateDraft(int id, [FromBody] UpdateLeaveRequestDto dto) =>
        Ok(await _service.UpdateDraftAsync(id, dto, Caller));

    /// <summary>Gửi duyệt: kiểm tra báo trước, trùng ngày, số dư; giữ chỗ PendingDays.</summary>
    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = AuthorizationPolicies.HasEmployee)]
    public async Task<ActionResult<LeaveRequestResponse>> Submit(int id) =>
        Ok(await _service.SubmitAsync(id, Caller));

    /// <summary>Duyệt đơn (quản lý trực tiếp hoặc Admin, không tự duyệt).</summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveLeave)]
    public async Task<ActionResult<LeaveRequestResponse>> Approve(int id, [FromBody] ApproveLeaveDto? dto) =>
        Ok(await _service.ApproveAsync(id, dto ?? new ApproveLeaveDto(), Caller));

    /// <summary>Từ chối đơn (bắt buộc ghi lý do).</summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveLeave)]
    public async Task<ActionResult<LeaveRequestResponse>> Reject(int id, [FromBody] RejectLeaveDto dto) =>
        Ok(await _service.RejectAsync(id, dto, Caller));

    /// <summary>Hủy đơn (chủ đơn hoặc Admin). Đơn đã duyệt chỉ hủy được trước ngày bắt đầu.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<LeaveRequestResponse>> Cancel(int id, [FromBody] CancelLeaveDto? dto) =>
        Ok(await _service.CancelAsync(id, dto ?? new CancelLeaveDto(), Caller));
}

/// <summary>Lịch sử duyệt/từ chối.</summary>
[ApiController]
[Route("api/leave-approvals")]
[Produces("application/json")]
[Authorize]
public class LeaveApprovalsController : ControllerBase
{
    private readonly ILeaveRequestService _service;

    public LeaveApprovalsController(ILeaveRequestService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<LeaveApprovalResponse>>> GetHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10) =>
        Ok(await _service.GetApprovalHistoryAsync(page, pageSize, CallerContext.From(User)));
}

/// <summary>Số dư phép theo năm.</summary>
[ApiController]
[Route("api/leave-balances")]
[Produces("application/json")]
[Authorize]
public class LeaveBalancesController : ControllerBase
{
    private readonly ILeaveBalanceService _service;

    public LeaveBalancesController(ILeaveBalanceService service)
    {
        _service = service;
    }

    /// <summary>Số dư trong phạm vi được xem.</summary>
    [HttpGet]
    public async Task<ActionResult<List<LeaveBalanceResponse>>> Get([FromQuery] LeaveBalanceQuery query) =>
        Ok(await _service.GetAsync(query, CallerContext.From(User)));

    /// <summary>Admin cấp số dư phép.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(LeaveBalanceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<LeaveBalanceResponse>> Create([FromBody] CreateLeaveBalanceDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Admin điều chỉnh số ngày được cấp.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<LeaveBalanceResponse>> Update(int id, [FromBody] UpdateLeaveBalanceDto dto) =>
        Ok(await _service.UpdateAsync(id, dto));
}

/// <summary>Tổng quan theo vai trò.</summary>
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get() =>
        Ok(await _service.GetAsync(CallerContext.From(User)));
}
