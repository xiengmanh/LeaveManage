using LeaveManage.Constants;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Exceptions;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public interface ILeaveRequestService
{
    Task<PaginatedResponse<LeaveRequestResponse>> GetListAsync(LeaveRequestQuery query, CallerContext caller);
    Task<PaginatedResponse<LeaveRequestResponse>> GetPendingForApprovalAsync(LeaveRequestQuery query, CallerContext caller);
    Task<LeaveRequestResponse> GetByIdAsync(int id, CallerContext caller);
    Task<LeaveRequestResponse> CreateAsync(CreateLeaveRequestDto dto, CallerContext caller);
    Task<LeaveRequestResponse> UpdateDraftAsync(int id, UpdateLeaveRequestDto dto, CallerContext caller);
    Task<LeaveRequestResponse> SubmitAsync(int id, CallerContext caller);
    Task<LeaveRequestResponse> ApproveAsync(int id, ApproveLeaveDto dto, CallerContext caller);
    Task<LeaveRequestResponse> RejectAsync(int id, RejectLeaveDto dto, CallerContext caller);
    Task<LeaveRequestResponse> CancelAsync(int id, CancelLeaveDto dto, CallerContext caller);
    Task<PaginatedResponse<LeaveApprovalResponse>> GetApprovalHistoryAsync(int page, int pageSize, CallerContext caller);
}

/// <summary>
/// Luồng nghiệp vụ đơn nghỉ:
/// Draft --submit--> Pending --approve--> Approved
///                          \--reject---> Rejected
/// Draft/Pending/Approved(chưa bắt đầu) --cancel--> Cancelled
/// Số dư (chỉ với loại nghỉ AllowsDeduction): submit giữ chỗ PendingDays; approve chuyển sang UsedDays;
/// reject/cancel trả lại.
/// </summary>
public class LeaveRequestService : ILeaveRequestService
{
    private readonly ILeaveRepository _repository;
    private readonly ILogger<LeaveRequestService> _logger;

    public LeaveRequestService(ILeaveRepository repository, ILogger<LeaveRequestService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // ===================== Đọc dữ liệu =====================

    public async Task<PaginatedResponse<LeaveRequestResponse>> GetListAsync(LeaveRequestQuery query, CallerContext caller)
    {
        var (items, total) = await _repository.GetRequestsPagedAsync(query, DataScope.For(caller));
        return ToPage(items, total, query);
    }

    public async Task<PaginatedResponse<LeaveRequestResponse>> GetPendingForApprovalAsync(LeaveRequestQuery query, CallerContext caller)
    {
        var scope = new DataScope(caller.IsAdmin, caller.RequireEmployeeId(), caller.IsManager);
        var (items, total) = await _repository.GetRequestsPagedAsync(query, scope, pendingForApprover: true);
        return ToPage(items, total, query);
    }

    public async Task<LeaveRequestResponse> GetByIdAsync(int id, CallerContext caller)
    {
        var request = await _repository.GetRequestDetailAsync(id);
        EnsureVisible(request, caller);
        return Map(request!);
    }

    public async Task<PaginatedResponse<LeaveApprovalResponse>> GetApprovalHistoryAsync(int page, int pageSize, CallerContext caller)
    {
        if (page < 1 || page > PagingLimits.MaxPage)
            throw new BadRequestException($"Số trang phải từ 1 đến {PagingLimits.MaxPage}.", "INVALID_PAGE");
        pageSize = Math.Clamp(pageSize, 1, 50);
        var (items, total) = await _repository.GetApprovalsPagedAsync(DataScope.For(caller), page, pageSize);
        return new PaginatedResponse<LeaveApprovalResponse>
        {
            Items = items.Select(MapApproval).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ===================== Tạo / sửa nháp =====================

    public async Task<LeaveRequestResponse> CreateAsync(CreateLeaveRequestDto dto, CallerContext caller)
    {
        var employeeId = caller.RequireEmployeeId();
        await EnsureActiveEmployeeAsync(employeeId);
        var (leaveType, start, end, days) = await ValidateRequestDataAsync(dto);

        var request = new LeaveRequest
        {
            RequestNumber = $"LR{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.Id,
            StartDate = start,
            EndDate = end,
            TotalDays = days,
            PaidDays = CalculatePaidDays(leaveType, days),
            Reason = dto.Reason.Trim(),
            ContactAddress = dto.ContactAddress?.Trim(),
            ContactPhone = dto.ContactPhone?.Trim(),
            Status = LeaveRequestStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddRequestAsync(request);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Employee {EmployeeId} created leave request {RequestId} ({Days} days)", employeeId, request.Id, days);
        return await GetByIdAsync(request.Id, caller);
    }

    public async Task<LeaveRequestResponse> UpdateDraftAsync(int id, UpdateLeaveRequestDto dto, CallerContext caller)
    {
        var employeeId = caller.RequireEmployeeId();

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            // Cùng giao thức với Submit/Approve/Cancel: khóa nhân viên TRƯỚC rồi mới đọc đơn.
            // Nếu Submit đang chạy, lệnh sửa phải chờ; sau khi có khóa sẽ thấy trạng thái mới nhất
            // (đã Pending → 409), nên TotalDays không bao giờ lệch với PendingDays đã giữ chỗ.
            await _repository.LockEmployeeAsync(employeeId);

            var request = await _repository.GetRequestTrackedAsync(id);
            EnsureOwner(request, caller);
            if (request!.Status != LeaveRequestStatus.Draft)
                throw new ConflictException("Chỉ sửa được đơn ở trạng thái Nháp.", "INVALID_STATUS");

            var (leaveType, start, end, days) = await ValidateRequestDataAsync(dto);
            request.LeaveTypeId = leaveType.Id;
            request.StartDate = start;
            request.EndDate = end;
            request.TotalDays = days;
            request.PaidDays = CalculatePaidDays(leaveType, days);
            request.Reason = dto.Reason.Trim();
            request.ContactAddress = dto.ContactAddress?.Trim();
            request.ContactPhone = dto.ContactPhone?.Trim();
            request.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync();
            return true;
        });

        return await GetByIdAsync(id, caller);
    }

    // ===================== Gửi duyệt =====================

    public async Task<LeaveRequestResponse> SubmitAsync(int id, CallerContext caller)
    {
        var employeeId = caller.RequireEmployeeId();

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            // Khóa nhân viên trước: 2 lần submit song song của cùng người sẽ chạy lần lượt
            await _repository.LockEmployeeAsync(employeeId);

            var request = await _repository.GetRequestTrackedAsync(id);
            EnsureOwner(request, caller);
            if (request!.Status != LeaveRequestStatus.Draft)
                throw new ConflictException("Chỉ gửi duyệt được đơn ở trạng thái Nháp.", "INVALID_STATUS");

            await EnsureActiveEmployeeAsync(employeeId);
            var leaveType = request.LeaveType!;
            if (!leaveType.IsActive)
                throw new BadRequestException("Loại nghỉ đã ngừng sử dụng.", "INVALID_LEAVE_TYPE");
            EnsureNotice(leaveType, request.StartDate);

            if (await _repository.HasOverlapAsync(employeeId, request.StartDate, request.EndDate, request.Id))
                throw new ConflictException("Khoảng ngày bị trùng với một đơn đang chờ duyệt hoặc đã duyệt.", "OVERLAPPING_REQUEST");

            var year = request.StartDate.Year;
            if (leaveType.AllowsDeduction)
            {
                var balance = await _repository.GetBalanceTrackedAsync(employeeId, leaveType.Id, year)
                    ?? throw new ConflictException($"Chưa được cấp số dư '{leaveType.Name}' cho năm {year}.", "NO_BALANCE");
                if (balance.RemainingDays < request.TotalDays)
                    throw new ConflictException(
                        $"Không đủ số dư: còn {balance.RemainingDays} ngày, đơn cần {request.TotalDays} ngày.", "INSUFFICIENT_BALANCE");

                balance.PendingDays += request.TotalDays; // giữ chỗ trong lúc chờ duyệt
                balance.UpdatedAt = DateTime.UtcNow;
            }
            else if (leaveType.DefaultDaysPerYear is int yearlyLimit)
            {
                // Loại không trừ số dư nhưng có giới hạn/năm (ví dụ nghỉ cưới 3 ngày)
                var alreadyUsed = await _repository.SumDaysInYearAsync(employeeId, leaveType.Id, year, request.Id);
                if (alreadyUsed + request.TotalDays > yearlyLimit)
                    throw new ConflictException(
                        $"Vượt giới hạn {yearlyLimit} ngày/năm của '{leaveType.Name}' (đã dùng/đang chờ {alreadyUsed} ngày).", "EXCEEDS_YEARLY_LIMIT");
            }

            request.Status = LeaveRequestStatus.Pending;
            request.SubmittedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync();
            return true;
        });

        _logger.LogInformation("Leave request {RequestId} submitted by employee {EmployeeId}", id, employeeId);
        return await GetByIdAsync(id, caller);
    }

    // ===================== Duyệt / Từ chối =====================

    public Task<LeaveRequestResponse> ApproveAsync(int id, ApproveLeaveDto dto, CallerContext caller) =>
        DecideAsync(id, approve: true, dto.Comment, caller);

    public Task<LeaveRequestResponse> RejectAsync(int id, RejectLeaveDto dto, CallerContext caller) =>
        DecideAsync(id, approve: false, dto.Comment, caller);

    private async Task<LeaveRequestResponse> DecideAsync(int id, bool approve, string? comment, CallerContext caller)
    {
        var approverId = caller.RequireEmployeeId();
        var ownerId = await _repository.GetRequestOwnerIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy đơn nghỉ.", "LEAVE_REQUEST_NOT_FOUND");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            // Cùng khóa với submit/cancel → hai người duyệt cùng lúc chỉ một người xử lý được
            await _repository.LockEmployeeAsync(ownerId);

            var request = await _repository.GetRequestTrackedAsync(id);
            EnsureVisible(request, caller);
            if (request!.EmployeeId == approverId)
                throw new ForbiddenException("Không được tự duyệt đơn của chính mình.", "SELF_APPROVAL");
            if (!caller.IsAdmin && request.Employee!.ManagerId != approverId)
                throw new ForbiddenException("Chỉ quản lý trực tiếp hoặc Admin được duyệt đơn này.", "NOT_DIRECT_MANAGER");
            if (request.Status != LeaveRequestStatus.Pending)
                throw new ConflictException(
                    $"Đơn đã được xử lý (trạng thái hiện tại: {request.Status}).", "ALREADY_PROCESSED");

            if (request.LeaveType!.AllowsDeduction)
            {
                var balance = await _repository.GetBalanceTrackedAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year)
                    ?? throw new ConflictException("Không tìm thấy số dư phép tương ứng.", "NO_BALANCE");
                balance.PendingDays -= request.TotalDays;
                if (approve) balance.UsedDays += request.TotalDays;
                balance.UpdatedAt = DateTime.UtcNow;
            }

            request.Status = approve ? LeaveRequestStatus.Approved : LeaveRequestStatus.Rejected;
            request.UpdatedAt = DateTime.UtcNow;
            await _repository.AddApprovalAsync(new LeaveApproval
            {
                LeaveRequestId = request.Id,
                ApproverId = approverId,
                ApprovalLevel = 1,
                Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected,
                Comment = comment?.Trim(),
                ActionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await _repository.SaveChangesAsync();
            return true;
        });

        _logger.LogInformation("Leave request {RequestId} {Decision} by employee {ApproverId}",
            id, approve ? "approved" : "rejected", approverId);
        return await GetByIdAsync(id, caller);
    }

    // ===================== Hủy =====================

    public async Task<LeaveRequestResponse> CancelAsync(int id, CancelLeaveDto dto, CallerContext caller)
    {
        var ownerId = await _repository.GetRequestOwnerIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy đơn nghỉ.", "LEAVE_REQUEST_NOT_FOUND");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.LockEmployeeAsync(ownerId);

            var request = await _repository.GetRequestTrackedAsync(id);
            if (!caller.IsAdmin)
                EnsureOwner(request, caller); // chỉ chủ đơn hoặc Admin được hủy

            var today = DateTime.Today;
            BalanceChange change = request!.Status switch
            {
                LeaveRequestStatus.Draft => BalanceChange.None,
                LeaveRequestStatus.Pending => BalanceChange.ReleasePending,
                LeaveRequestStatus.Approved when request.StartDate.Date > today => BalanceChange.ReturnUsed,
                LeaveRequestStatus.Approved => throw new ConflictException(
                    "Đơn đã duyệt chỉ hủy được trước ngày bắt đầu nghỉ.", "CANNOT_CANCEL_STARTED"),
                _ => throw new ConflictException(
                    $"Không thể hủy đơn ở trạng thái {request.Status}.", "INVALID_STATUS")
            };

            if (change != BalanceChange.None && request.LeaveType!.AllowsDeduction)
            {
                var balance = await _repository.GetBalanceTrackedAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
                if (balance is not null)
                {
                    if (change == BalanceChange.ReleasePending) balance.PendingDays -= request.TotalDays;
                    else balance.UsedDays -= request.TotalDays;
                    balance.UpdatedAt = DateTime.UtcNow;
                }
            }

            request.Status = LeaveRequestStatus.Cancelled;
            request.CancelledAt = DateTime.UtcNow;
            request.CancelReason = dto.Reason?.Trim();
            request.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync();
            return true;
        });

        _logger.LogInformation("Leave request {RequestId} cancelled by user {UserId}", id, caller.UserId);
        return await GetByIdAsync(id, caller);
    }

    private enum BalanceChange { None, ReleasePending, ReturnUsed }

    // ===================== Kiểm tra dùng chung =====================

    private async Task<(LeaveType LeaveType, DateTime Start, DateTime End, int Days)> ValidateRequestDataAsync(CreateLeaveRequestDto dto)
    {
        var start = dto.StartDate!.Value.Date;
        var end = dto.EndDate!.Value.Date;
        if (end < start)
            throw new BadRequestException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.", "INVALID_DATE_RANGE");
        if (start.Year != end.Year)
            throw new BadRequestException("Đơn nghỉ không được kéo dài sang năm khác; hãy tách thành 2 đơn.", "CROSS_YEAR_REQUEST");

        var leaveType = await _repository.GetLeaveTypeAsync(dto.LeaveTypeId);
        if (leaveType is null || !leaveType.IsActive)
            throw new BadRequestException("Loại nghỉ không tồn tại hoặc đã ngừng sử dụng.", "INVALID_LEAVE_TYPE");

        EnsureNotice(leaveType, start);

        var holidays = await _repository.GetHolidayDatesAsync(start, end);
        var days = LeaveDayCalculator.CountWorkingDays(start, end, holidays);
        if (days == 0)
            throw new BadRequestException("Khoảng nghỉ không có ngày làm việc nào (chỉ gồm Thứ 7/Chủ nhật).", "NO_WORKING_DAYS");
        if (leaveType.DefaultDaysPerYear is int limit && days > limit)
            throw new BadRequestException($"Loại '{leaveType.Name}' tối đa {limit} ngày/năm.", "EXCEEDS_YEARLY_LIMIT");

        return (leaveType, start, end, days);
    }

    private static void EnsureNotice(LeaveType leaveType, DateTime start)
    {
        var earliest = DateTime.Today.AddDays(leaveType.NoticeDaysRequired ?? 0);
        if (start.Date < earliest)
            throw new BadRequestException(
                $"Loại '{leaveType.Name}' phải báo trước {leaveType.NoticeDaysRequired ?? 0} ngày (sớm nhất từ {earliest:dd/MM/yyyy}).",
                "NOTICE_REQUIRED");
    }

    /// <summary>Nghỉ không giới hạn/năm (DefaultDaysPerYear = null, ví dụ nghỉ không lương) thì không hưởng lương.</summary>
    private static int CalculatePaidDays(LeaveType leaveType, int days) =>
        leaveType.DefaultDaysPerYear is null ? 0 : days;

    private async Task EnsureActiveEmployeeAsync(int employeeId)
    {
        var employee = await _repository.GetEmployeeAsync(employeeId);
        if (employee is null || !employee.IsActive)
            throw new ForbiddenException("Nhân viên không tồn tại hoặc đã nghỉ việc.", "EMPLOYEE_INACTIVE");
    }

    /// <summary>Ngoài phạm vi được xem thì báo 404 (không tiết lộ đơn có tồn tại).</summary>
    private static void EnsureVisible(LeaveRequest? request, CallerContext caller)
    {
        if (request is null)
            throw new NotFoundException("Không tìm thấy đơn nghỉ.", "LEAVE_REQUEST_NOT_FOUND");
        if (caller.IsAdmin) return;
        var me = caller.RequireEmployeeId();
        var visible = request.EmployeeId == me
            || (caller.IsManager && request.Employee?.ManagerId == me);
        if (!visible)
            throw new NotFoundException("Không tìm thấy đơn nghỉ.", "LEAVE_REQUEST_NOT_FOUND");
    }

    private static void EnsureOwner(LeaveRequest? request, CallerContext caller)
    {
        EnsureVisible(request, caller);
        if (request!.EmployeeId != caller.EmployeeId)
            throw new ForbiddenException("Bạn chỉ được thao tác trên đơn của chính mình.", "NOT_OWNER");
    }

    // ===================== Mapping =====================

    private static PaginatedResponse<LeaveRequestResponse> ToPage(List<LeaveRequest> items, int total, LeaveRequestQuery query) => new()
    {
        Items = items.Select(Map).ToList(),
        TotalCount = total,
        Page = query.Page,
        PageSize = query.PageSize
    };

    private static LeaveRequestResponse Map(LeaveRequest r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        EmployeeId = r.EmployeeId,
        EmployeeCode = r.Employee?.EmployeeCode,
        EmployeeName = r.Employee?.FullName,
        LeaveTypeId = r.LeaveTypeId,
        LeaveTypeName = r.LeaveType?.Name,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
        TotalDays = r.TotalDays,
        PaidDays = r.PaidDays,
        Reason = r.Reason,
        ContactAddress = r.ContactAddress,
        ContactPhone = r.ContactPhone,
        Status = r.Status.ToString(),
        CreatedAt = r.CreatedAt,
        SubmittedAt = r.SubmittedAt,
        CancelledAt = r.CancelledAt,
        CancelReason = r.CancelReason,
        Approvals = r.Approvals.Select(MapApproval).ToList()
    };

    private static LeaveApprovalResponse MapApproval(LeaveApproval a) => new()
    {
        Id = a.Id,
        LeaveRequestId = a.LeaveRequestId,
        RequestNumber = a.LeaveRequest?.RequestNumber,
        Status = a.Status.ToString(),
        Comment = a.Comment,
        ActionDate = a.ActionDate,
        ApproverId = a.ApproverId,
        ApproverName = a.Approver?.FullName
    };
}
