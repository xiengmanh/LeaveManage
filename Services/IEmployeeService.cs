using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;

namespace LeaveManage.Services;

public interface IEmployeeService
{
    Task<PaginatedResponse<EmployeeResponse>> GetAllAsync(EmployeeQueryRequest query, CallerContext caller);
    /// <summary>Trả null nếu không tồn tại HOẶC nằm ngoài phạm vi được xem (tránh lộ dữ liệu).</summary>
    Task<EmployeeResponse?> GetByIdAsync(int id, CallerContext caller, bool includeLeaveBalances = false);
    Task<(bool Success, string Message, string? ErrorCode, EmployeeResponse? Data)>
        CreateAsync(CreateEmployeeRequest request);
    Task<(bool Success, string Message, string? ErrorCode, EmployeeResponse? Data)>
        UpdateAsync(int id, UpdateEmployeeRequest request);
    Task<(bool Success, string Message, string? ErrorCode)> DeleteAsync(int id);
    Task<List<DepartmentResponse>> GetDepartmentsAsync();
    Task<List<PositionResponse>> GetPositionsAsync();
    Task<DepartmentEmployeesResponse?> GetDepartmentEmployeesAsync(int id);
}
