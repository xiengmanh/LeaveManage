using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Exceptions;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public interface ILeaveBalanceService
{
    Task<List<LeaveBalanceResponse>> GetAsync(LeaveBalanceQuery query, CallerContext caller);
    Task<LeaveBalanceResponse> CreateAsync(CreateLeaveBalanceDto dto);
    Task<LeaveBalanceResponse> UpdateAsync(int id, UpdateLeaveBalanceDto dto);
}

public class LeaveBalanceService : ILeaveBalanceService
{
    private readonly ILeaveRepository _repository;

    public LeaveBalanceService(ILeaveRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<LeaveBalanceResponse>> GetAsync(LeaveBalanceQuery query, CallerContext caller) =>
        (await _repository.GetBalancesAsync(query, DataScope.For(caller))).Select(Map).ToList();

    /// <summary>Admin cấp số dư phép cho một nhân viên theo loại nghỉ và năm.</summary>
    public async Task<LeaveBalanceResponse> CreateAsync(CreateLeaveBalanceDto dto)
    {
        if (await _repository.GetEmployeeAsync(dto.EmployeeId) is null)
            throw new NotFoundException("Không tìm thấy nhân viên.", "EMPLOYEE_NOT_FOUND");
        var leaveType = await _repository.GetLeaveTypeAsync(dto.LeaveTypeId)
            ?? throw new NotFoundException("Không tìm thấy loại nghỉ.", "LEAVE_TYPE_NOT_FOUND");
        if (!leaveType.AllowsDeduction)
            throw new BadRequestException("Loại nghỉ này không trừ số dư nên không cần cấp số dư.", "LEAVE_TYPE_NO_DEDUCTION");
        if (await _repository.BalanceExistsAsync(dto.EmployeeId, dto.LeaveTypeId, dto.Year))
            throw new ConflictException("Nhân viên đã có số dư cho loại nghỉ và năm này.", "DUPLICATE_BALANCE");

        var balance = new LeaveBalance
        {
            EmployeeId = dto.EmployeeId,
            LeaveTypeId = dto.LeaveTypeId,
            Year = dto.Year,
            InitialDays = dto.InitialDays,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddBalanceAsync(balance);
        await _repository.SaveChangesAsync(); // unique (EmployeeId, LeaveTypeId, Year) chặn trường hợp tạo trùng song song → 409
        return Map((await _repository.GetBalanceByIdAsync(balance.Id))!);
    }

    /// <summary>Admin điều chỉnh số ngày được cấp; không được thấp hơn số đã dùng + đang chờ.</summary>
    public async Task<LeaveBalanceResponse> UpdateAsync(int id, UpdateLeaveBalanceDto dto)
    {
        // Đọc không tracking để sau khi khóa, lần đọc tracked bên dưới lấy giá trị mới nhất từ DB
        var current = await _repository.GetBalanceByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy số dư phép.", "BALANCE_NOT_FOUND");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            // Khóa cùng nhân viên để không đụng độ với submit/duyệt đang chạy
            await _repository.LockEmployeeAsync(current.EmployeeId);
            var balance = (await _repository.GetBalanceTrackedAsync(current.EmployeeId, current.LeaveTypeId, current.Year))!;
            if (dto.InitialDays < balance.UsedDays + balance.PendingDays)
                throw new ConflictException(
                    $"Số ngày cấp không được nhỏ hơn số đã dùng + đang chờ ({balance.UsedDays + balance.PendingDays}).",
                    "BALANCE_BELOW_USAGE");
            balance.InitialDays = dto.InitialDays;
            balance.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync();
            return true;
        });

        return Map((await _repository.GetBalanceByIdAsync(id))!);
    }

    private static LeaveBalanceResponse Map(LeaveBalance b) => new()
    {
        Id = b.Id,
        EmployeeId = b.EmployeeId,
        EmployeeCode = b.Employee?.EmployeeCode,
        EmployeeName = b.Employee?.FullName,
        LeaveTypeId = b.LeaveTypeId,
        LeaveTypeName = b.LeaveType?.Name,
        Year = b.Year,
        InitialDays = b.InitialDays,
        UsedDays = b.UsedDays,
        PendingDays = b.PendingDays,
        RemainingDays = b.RemainingDays
    };
}
