using LeaveManage.DTOs.Requests;
using LeaveManage.Models;
using LeaveManage.Services;

namespace LeaveManage.Repositories;

public interface IEmployeeRepository
{
    Task<(List<Employee> Items, int TotalCount)> GetPagedAsync(EmployeeQueryRequest query, DataScope scope);
    /// <summary>Kiểm tra candidateManagerId có phải là cấp dưới (trực tiếp/gián tiếp) của employeeId không, để chặn vòng lặp quản lý.</summary>
    Task<bool> IsInReportingChainAsync(int employeeId, int candidateManagerId);
    Task<Employee?> GetByIdAsync(int id, bool includeLeaveBalances = false);
    Task<Employee?> GetTrackedByIdAsync(int id);
    Task<bool> EmployeeCodeExistsAsync(string employeeCode);
    Task<bool> EmailExistsAsync(string email, int? exceptEmployeeId = null);
    Task<bool> DepartmentExistsAsync(int id);
    Task<bool> PositionExistsAsync(int id);
    Task<bool> EmployeeExistsAsync(int id);
    Task AddAsync(Employee employee);
    Task<bool> SaveChangesAsync();
    Task<List<Department>> GetActiveDepartmentsAsync();
    Task<List<Position>> GetActivePositionsAsync();
    Task<Department?> GetDepartmentWithEmployeesAsync(int id);
}
