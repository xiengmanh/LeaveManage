using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeeService(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<PaginatedResponse<EmployeeResponse>> GetAllAsync(
        EmployeeQueryRequest query, CallerContext caller)
    {
        var (items, totalCount) = await _employeeRepository.GetPagedAsync(query, DataScope.For(caller));
        return new PaginatedResponse<EmployeeResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<EmployeeResponse?> GetByIdAsync(
        int id,
        CallerContext caller,
        bool includeLeaveBalances = false)
    {
        var employee = await _employeeRepository.GetByIdAsync(id, includeLeaveBalances);
        if (employee is null)
            return null;

        var scope = DataScope.For(caller);
        var allowed = scope.AllEmployees
            || employee.Id == scope.EmployeeId
            || (scope.IncludeDirectReports && employee.ManagerId == scope.EmployeeId);
        return allowed ? MapToResponse(employee) : null;
    }

    public async Task<(bool, string, string?, EmployeeResponse?)> CreateAsync(
        CreateEmployeeRequest request)
    {
        if (await _employeeRepository.EmployeeCodeExistsAsync(request.EmployeeCode))
            return (false, "Mã nhân viên đã tồn tại", "DUPLICATE_CODE", null);

        if (await _employeeRepository.EmailExistsAsync(request.Email))
            return (false, "Email đã tồn tại", "DUPLICATE_EMAIL", null);

        var referenceError = await ValidateReferencesAsync(
            request.DepartmentId,
            request.PositionId,
            request.ManagerId);
        if (referenceError.HasValue)
            return (false, referenceError.Value.Message, referenceError.Value.Code, null);

        var employee = new Employee
        {
            EmployeeCode = request.EmployeeCode.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            HireDate = request.HireDate,
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            ManagerId = request.ManagerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _employeeRepository.AddAsync(employee);
        await _employeeRepository.SaveChangesAsync();
        var created = await _employeeRepository.GetByIdAsync(employee.Id);
        return (true, "Tạo nhân viên thành công", null, MapToResponse(created!));
    }

    public async Task<(bool, string, string?, EmployeeResponse?)> UpdateAsync(
        int id,
        UpdateEmployeeRequest request)
    {
        var employee = await _employeeRepository.GetTrackedByIdAsync(id);
        if (employee is null)
            return (false, "Không tìm thấy nhân viên", "NOT_FOUND", null);

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            await _employeeRepository.EmailExistsAsync(request.Email, id))
        {
            return (false, "Email đã tồn tại", "DUPLICATE_EMAIL", null);
        }

        if (request.DepartmentId.HasValue &&
            !await _employeeRepository.DepartmentExistsAsync(request.DepartmentId.Value))
            return (false, "Phòng ban không tồn tại", "INVALID_DEPARTMENT", null);

        if (request.PositionId.HasValue &&
            !await _employeeRepository.PositionExistsAsync(request.PositionId.Value))
            return (false, "Chức vụ không tồn tại", "INVALID_POSITION", null);

        if (request.ManagerId.HasValue && request.ManagerId.Value != 0)
        {
            if (request.ManagerId.Value == id)
                return (false, "Nhân viên không thể tự quản lý chính mình", "INVALID_MANAGER", null);
            if (!await _employeeRepository.EmployeeExistsAsync(request.ManagerId.Value))
                return (false, "Quản lý không tồn tại", "INVALID_MANAGER", null);
            if (await _employeeRepository.IsInReportingChainAsync(id, request.ManagerId.Value))
                return (false, "Không thể chọn cấp dưới làm quản lý (tạo vòng lặp)", "INVALID_MANAGER", null);
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
            employee.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email))
            employee.Email = request.Email.Trim();
        if (request.PhoneNumber is not null)
            employee.PhoneNumber = request.PhoneNumber;
        if (request.DateOfBirth.HasValue)
            employee.DateOfBirth = request.DateOfBirth.Value;
        if (request.DepartmentId.HasValue)
            employee.DepartmentId = request.DepartmentId.Value;
        if (request.PositionId.HasValue)
            employee.PositionId = request.PositionId.Value;
        if (request.ManagerId.HasValue)
            employee.ManagerId = request.ManagerId.Value == 0 ? null : request.ManagerId.Value;
        if (request.IsActive.HasValue)
            employee.IsActive = request.IsActive.Value;

        employee.UpdatedAt = DateTime.UtcNow;
        await _employeeRepository.SaveChangesAsync();
        var updated = await _employeeRepository.GetByIdAsync(id);
        return (true, "Cập nhật nhân viên thành công", null, MapToResponse(updated!));
    }

    public async Task<(bool, string, string?)> DeleteAsync(int id)
    {
        var employee = await _employeeRepository.GetTrackedByIdAsync(id);
        if (employee is null)
            return (false, "Không tìm thấy nhân viên", "NOT_FOUND");

        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        await _employeeRepository.SaveChangesAsync();
        return (true, "Xóa nhân viên thành công", null);
    }

    public async Task<List<DepartmentResponse>> GetDepartmentsAsync() =>
        (await _employeeRepository.GetActiveDepartmentsAsync())
        .Select(MapDepartment)
        .ToList();

    public async Task<List<PositionResponse>> GetPositionsAsync() =>
        (await _employeeRepository.GetActivePositionsAsync())
        .Select(MapPosition)
        .ToList();

    public async Task<DepartmentEmployeesResponse?> GetDepartmentEmployeesAsync(int id)
    {
        var department = await _employeeRepository.GetDepartmentWithEmployeesAsync(id);
        if (department is null)
            return null;

        return new DepartmentEmployeesResponse
        {
            Department = MapDepartment(department),
            Employees = department.Employees
                .OrderBy(employee => employee.FullName)
                .Select(employee => new EmployeeBasicResponse
                {
                    Id = employee.Id,
                    EmployeeCode = employee.EmployeeCode,
                    FullName = employee.FullName,
                    Email = employee.Email
                })
                .ToList()
        };
    }

    private async Task<(string Message, string Code)?> ValidateReferencesAsync(
        int departmentId,
        int positionId,
        int? managerId)
    {
        if (!await _employeeRepository.DepartmentExistsAsync(departmentId))
            return ("Phòng ban không tồn tại", "INVALID_DEPARTMENT");
        if (!await _employeeRepository.PositionExistsAsync(positionId))
            return ("Chức vụ không tồn tại", "INVALID_POSITION");
        if (managerId.HasValue && !await _employeeRepository.EmployeeExistsAsync(managerId.Value))
            return ("Quản lý không tồn tại", "INVALID_MANAGER");
        return null;
    }

    private static EmployeeResponse MapToResponse(Employee employee) => new()
    {
        Id = employee.Id,
        EmployeeCode = employee.EmployeeCode,
        FullName = employee.FullName,
        Email = employee.Email,
        PhoneNumber = employee.PhoneNumber,
        DateOfBirth = employee.DateOfBirth,
        HireDate = employee.HireDate,
        IsActive = employee.IsActive,
        CreatedAt = employee.CreatedAt,
        Department = employee.Department is null ? null : MapDepartment(employee.Department),
        Position = employee.Position is null ? null : MapPosition(employee.Position),
        Manager = employee.Manager is null ? null : new EmployeeBasicResponse
        {
            Id = employee.Manager.Id,
            EmployeeCode = employee.Manager.EmployeeCode,
            FullName = employee.Manager.FullName,
            Email = employee.Manager.Email
        }
    };

    private static DepartmentResponse MapDepartment(Department department) => new()
    {
        Id = department.Id,
        Name = department.Name,
        Code = department.Code,
        Description = department.Description,
        IsActive = department.IsActive
    };

    private static PositionResponse MapPosition(Position position) => new()
    {
        Id = position.Id,
        Name = position.Name,
        Code = position.Code,
        Level = position.Level,
        IsActive = position.IsActive
    };
}
