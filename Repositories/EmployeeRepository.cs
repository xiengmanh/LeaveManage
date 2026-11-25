using LeaveManage.Data;
using LeaveManage.DTOs.Requests;
using LeaveManage.Models;
using LeaveManage.Services;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _context;

    public EmployeeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Employee> Items, int TotalCount)> GetPagedAsync(
        EmployeeQueryRequest request, DataScope scope)
    {
        var query = _context.Employees
            .AsNoTracking()
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .Include(employee => employee.Manager)
            .AsQueryable();

        // Phạm vi dữ liệu: User chỉ thấy mình, Manager thấy mình + cấp dưới trực tiếp, Admin thấy tất cả
        if (!scope.AllEmployees)
        {
            query = query.Where(employee => employee.Id == scope.EmployeeId
                || (scope.IncludeDirectReports && employee.ManagerId == scope.EmployeeId));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(employee =>
                employee.FullName.ToLower().Contains(search) ||
                employee.EmployeeCode.ToLower().Contains(search) ||
                employee.Email.ToLower().Contains(search));
        }

        if (request.DepartmentId.HasValue)
            query = query.Where(employee => employee.DepartmentId == request.DepartmentId);

        if (request.PositionId.HasValue)
            query = query.Where(employee => employee.PositionId == request.PositionId);

        if (request.IsActive.HasValue)
            query = query.Where(employee => employee.IsActive == request.IsActive);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(employee => employee.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<Employee?> GetByIdAsync(int id, bool includeLeaveBalances = false)
    {
        IQueryable<Employee> query = _context.Employees
            .AsNoTracking()
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .Include(employee => employee.Manager);

        if (includeLeaveBalances)
        {
            query = query
                .Include(employee => employee.LeaveBalances)
                .ThenInclude(balance => balance.LeaveType);
        }

        return query.FirstOrDefaultAsync(employee => employee.Id == id);
    }

    public async Task<bool> IsInReportingChainAsync(int employeeId, int candidateManagerId)
    {
        // Đi ngược từ candidateManager lên trên theo ManagerId; nếu gặp employeeId thì sẽ tạo vòng lặp
        var currentId = (int?)candidateManagerId;
        for (var depth = 0; currentId.HasValue && depth < 50; depth++)
        {
            if (currentId.Value == employeeId) return true;
            currentId = await _context.Employees
                .Where(employee => employee.Id == currentId.Value)
                .Select(employee => employee.ManagerId)
                .FirstOrDefaultAsync();
        }
        return false;
    }

    public Task<Employee?> GetTrackedByIdAsync(int id)
    {
        return _context.Employees.FirstOrDefaultAsync(employee => employee.Id == id);
    }

    public Task<bool> EmployeeCodeExistsAsync(string employeeCode)
    {
        return _context.Employees.AnyAsync(employee => employee.EmployeeCode == employeeCode);
    }

    public Task<bool> EmailExistsAsync(string email, int? exceptEmployeeId = null)
    {
        return _context.Employees.AnyAsync(employee =>
            employee.Email == email &&
            (!exceptEmployeeId.HasValue || employee.Id != exceptEmployeeId));
    }

    public Task<bool> DepartmentExistsAsync(int id) =>
        _context.Departments.AnyAsync(department => department.Id == id);

    public Task<bool> PositionExistsAsync(int id) =>
        _context.Positions.AnyAsync(position => position.Id == id);

    public Task<bool> EmployeeExistsAsync(int id) =>
        _context.Employees.AnyAsync(employee => employee.Id == id);

    public Task AddAsync(Employee employee) =>
        _context.Employees.AddAsync(employee).AsTask();

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;

    public Task<List<Department>> GetActiveDepartmentsAsync() =>
        _context.Departments
            .AsNoTracking()
            .Where(department => department.IsActive)
            .OrderBy(department => department.Name)
            .ToListAsync();

    public Task<List<Position>> GetActivePositionsAsync() =>
        _context.Positions
            .AsNoTracking()
            .Where(position => position.IsActive)
            .OrderBy(position => position.Level)
            .ToListAsync();

    public Task<Department?> GetDepartmentWithEmployeesAsync(int id) =>
        _context.Departments
            .AsNoTracking()
            .Include(department => department.Employees)
            .FirstOrDefaultAsync(department => department.Id == id);
}
