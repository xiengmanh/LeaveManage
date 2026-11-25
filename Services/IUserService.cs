using LeaveManage.DTOs.Common;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;

namespace LeaveManage.Services;

public interface IUserService
{
    Task<PagedResult<UserResponseDto>> GetAllAsync(
        UserQueryParameters queryParams);
    Task<UserResponseDto?> GetByIdAsync(int id);
    Task<(bool IsSuccess, string Message, string? ErrorCode, UserResponseDto? Data)>
        RegisterAsync(UserRegisterDto request);
    Task<(bool IsSuccess, string Message, string? ErrorCode, UserResponseDto? Data)>
        UpdateAsync(int id, UserUpdateDto request);
    Task<(bool IsSuccess, string Message, string? ErrorCode)>
        DeleteAsync(int id);
}
