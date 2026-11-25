using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Exceptions;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public interface ICatalogService
{
    Task<List<DepartmentResponse>> GetDepartmentsAsync(bool includeInactive);
    Task<DepartmentResponse> GetDepartmentAsync(int id);
    Task<DepartmentResponse> CreateDepartmentAsync(DepartmentUpsertDto dto);
    Task<DepartmentResponse> UpdateDepartmentAsync(int id, DepartmentUpsertDto dto);

    Task<List<PositionResponse>> GetPositionsAsync(bool includeInactive);
    Task<PositionResponse> GetPositionAsync(int id);
    Task<PositionResponse> CreatePositionAsync(PositionUpsertDto dto);
    Task<PositionResponse> UpdatePositionAsync(int id, PositionUpsertDto dto);

    Task<List<LeaveTypeResponse>> GetLeaveTypesAsync(bool includeInactive);
    Task<LeaveTypeResponse> GetLeaveTypeAsync(int id);
    Task<LeaveTypeResponse> CreateLeaveTypeAsync(LeaveTypeUpsertDto dto);
    Task<LeaveTypeResponse> UpdateLeaveTypeAsync(int id, LeaveTypeUpsertDto dto);

    /// <summary>"Xóa" danh mục = ngừng sử dụng (IsActive = false), giữ lại để không mất lịch sử.</summary>
    Task DeactivateDepartmentAsync(int id);
    Task DeactivatePositionAsync(int id);
    Task DeactivateLeaveTypeAsync(int id);
}

public class CatalogService : ICatalogService
{
    private readonly ICatalogRepository<Department> _departments;
    private readonly ICatalogRepository<Position> _positions;
    private readonly ICatalogRepository<LeaveType> _leaveTypes;

    public CatalogService(
        ICatalogRepository<Department> departments,
        ICatalogRepository<Position> positions,
        ICatalogRepository<LeaveType> leaveTypes)
    {
        _departments = departments;
        _positions = positions;
        _leaveTypes = leaveTypes;
    }

    // ---------- Department ----------
    public async Task<List<DepartmentResponse>> GetDepartmentsAsync(bool includeInactive) =>
        (await _departments.GetAllAsync(includeInactive)).Select(MapDepartment).ToList();

    public async Task<DepartmentResponse> GetDepartmentAsync(int id) =>
        MapDepartment(await _departments.GetByIdAsync(id) ?? throw NotFound("phòng ban"));

    public async Task<DepartmentResponse> CreateDepartmentAsync(DepartmentUpsertDto dto)
    {
        var code = await EnsureUniqueCodeAsync(_departments, dto.Code, null);
        var entity = new Department { Code = code, CreatedAt = DateTime.UtcNow };
        Apply(entity, dto);
        await _departments.AddAsync(entity);
        await _departments.SaveChangesAsync();
        return MapDepartment(entity);
    }

    public async Task<DepartmentResponse> UpdateDepartmentAsync(int id, DepartmentUpsertDto dto)
    {
        var entity = await _departments.GetTrackedByIdAsync(id) ?? throw NotFound("phòng ban");
        entity.Code = await EnsureUniqueCodeAsync(_departments, dto.Code, id);
        Apply(entity, dto);
        await _departments.SaveChangesAsync();
        return MapDepartment(entity);
    }

    // ---------- Position ----------
    public async Task<List<PositionResponse>> GetPositionsAsync(bool includeInactive) =>
        (await _positions.GetAllAsync(includeInactive)).OrderBy(p => p.Level).Select(MapPosition).ToList();

    public async Task<PositionResponse> GetPositionAsync(int id) =>
        MapPosition(await _positions.GetByIdAsync(id) ?? throw NotFound("chức vụ"));

    public async Task<PositionResponse> CreatePositionAsync(PositionUpsertDto dto)
    {
        var code = await EnsureUniqueCodeAsync(_positions, dto.Code, null);
        var entity = new Position { Code = code, CreatedAt = DateTime.UtcNow };
        Apply(entity, dto);
        await _positions.AddAsync(entity);
        await _positions.SaveChangesAsync();
        return MapPosition(entity);
    }

    public async Task<PositionResponse> UpdatePositionAsync(int id, PositionUpsertDto dto)
    {
        var entity = await _positions.GetTrackedByIdAsync(id) ?? throw NotFound("chức vụ");
        entity.Code = await EnsureUniqueCodeAsync(_positions, dto.Code, id);
        Apply(entity, dto);
        await _positions.SaveChangesAsync();
        return MapPosition(entity);
    }

    // ---------- LeaveType ----------
    public async Task<List<LeaveTypeResponse>> GetLeaveTypesAsync(bool includeInactive) =>
        (await _leaveTypes.GetAllAsync(includeInactive)).Select(MapLeaveType).ToList();

    public async Task<LeaveTypeResponse> GetLeaveTypeAsync(int id) =>
        MapLeaveType(await _leaveTypes.GetByIdAsync(id) ?? throw NotFound("loại nghỉ"));

    public async Task<LeaveTypeResponse> CreateLeaveTypeAsync(LeaveTypeUpsertDto dto)
    {
        var code = await EnsureUniqueCodeAsync(_leaveTypes, dto.Code, null);
        var entity = new LeaveType { Code = code, RequiresApproval = true, CreatedAt = DateTime.UtcNow };
        Apply(entity, dto);
        await _leaveTypes.AddAsync(entity);
        await _leaveTypes.SaveChangesAsync();
        return MapLeaveType(entity);
    }

    public async Task<LeaveTypeResponse> UpdateLeaveTypeAsync(int id, LeaveTypeUpsertDto dto)
    {
        var entity = await _leaveTypes.GetTrackedByIdAsync(id) ?? throw NotFound("loại nghỉ");
        if (entity.AllowsDeduction != dto.AllowsDeduction)
            // Đổi quy tắc trừ phép khi đã có đơn sẽ làm lệch số dư → không cho đổi
            throw new ConflictException("Không được đổi cờ 'trừ số dư' của loại nghỉ đã tạo.", "IMMUTABLE_DEDUCTION_RULE");
        entity.Code = await EnsureUniqueCodeAsync(_leaveTypes, dto.Code, id);
        Apply(entity, dto);
        await _leaveTypes.SaveChangesAsync();
        return MapLeaveType(entity);
    }

    // ---------- Dùng chung ----------
    public Task DeactivateDepartmentAsync(int id) => DeactivateAsync(_departments, id);
    public Task DeactivatePositionAsync(int id) => DeactivateAsync(_positions, id);
    public Task DeactivateLeaveTypeAsync(int id) => DeactivateAsync(_leaveTypes, id);

    private static async Task DeactivateAsync<T>(ICatalogRepository<T> repository, int id) where T : class, ICatalogEntity
    {
        var entity = await repository.GetTrackedByIdAsync(id) ?? throw NotFound("bản ghi");
        entity.IsActive = false;
        await repository.SaveChangesAsync();
    }

    private static async Task<string> EnsureUniqueCodeAsync<T>(ICatalogRepository<T> repository, string code, int? exceptId)
        where T : class, ICatalogEntity
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalized, exceptId))
            throw new ConflictException($"Mã '{normalized}' đã tồn tại.", "DUPLICATE_CODE");
        return normalized;
    }

    private static NotFoundException NotFound(string what) => new($"Không tìm thấy {what}.");

    private static void Apply(Department e, DepartmentUpsertDto d)
    {
        e.Name = d.Name.Trim();
        e.Description = d.Description?.Trim();
        e.IsActive = d.IsActive;
    }

    private static void Apply(Position e, PositionUpsertDto d)
    {
        e.Name = d.Name.Trim();
        e.Level = d.Level;
        e.IsActive = d.IsActive;
    }

    private static void Apply(LeaveType e, LeaveTypeUpsertDto d)
    {
        e.Name = d.Name.Trim();
        e.Description = d.Description?.Trim();
        e.DefaultDaysPerYear = d.DefaultDaysPerYear;
        e.NoticeDaysRequired = d.NoticeDaysRequired;
        e.AllowsDeduction = d.AllowsDeduction;
        e.IsActive = d.IsActive;
    }

    private static DepartmentResponse MapDepartment(Department d) => new()
    { Id = d.Id, Name = d.Name, Code = d.Code, Description = d.Description, IsActive = d.IsActive };

    private static PositionResponse MapPosition(Position p) => new()
    { Id = p.Id, Name = p.Name, Code = p.Code, Level = p.Level, IsActive = p.IsActive };

    private static LeaveTypeResponse MapLeaveType(LeaveType t) => new()
    {
        Id = t.Id, Name = t.Name, Code = t.Code, Description = t.Description,
        DefaultDaysPerYear = t.DefaultDaysPerYear, NoticeDaysRequired = t.NoticeDaysRequired,
        RequiresApproval = t.RequiresApproval, AllowsDeduction = t.AllowsDeduction, IsActive = t.IsActive
    };
}
