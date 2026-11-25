using LeaveManage.Constants;
using LeaveManage.DTOs.Common;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;

    public UserService(IUserRepository userRepository, IPasswordService passwordService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
    }

    public async Task<PagedResult<UserResponseDto>> GetAllAsync(
        UserQueryParameters queryParams)
    {
        var pagedUsers = await _userRepository.GetAllAsync(queryParams);

        // Map danh sách Entity sang DTO
        var mappedItems = pagedUsers.Items.Select(MapToResponseDto).ToList();

        // Trả về PagedResult chứa DTO
        return new PagedResult<UserResponseDto>(
            mappedItems,
            pagedUsers.TotalCount,
            pagedUsers.PageNumber,
            pagedUsers.PageSize);
    }

    public async Task<UserResponseDto?> GetByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user is null ? null : MapToResponseDto(user);
    }

    public async Task<(
        bool IsSuccess,
        string Message,
        string? ErrorCode,
        UserResponseDto? Data)> RegisterAsync(UserRegisterDto request)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        var existingUser = await _userRepository
            .GetByUsernameOrEmailAsync(username, email);

        if (existingUser is not null)
        {
            return (
                false,
                "Username hoặc Email đã được sử dụng.",
                "USER_ALREADY_EXISTS",
                null);
        }

        var role = UserRoles.User;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!UserRoles.All.Contains(request.Role.Trim()))
                return (false, "Role chỉ được phép là Admin, Manager hoặc User.", "INVALID_ROLE", null);
            role = UserRoles.All.First(item => item.Equals(request.Role.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var linkError = await ValidateEmployeeLinkAsync(request.EmployeeId, null);
        if (linkError is not null)
            return (false, linkError.Value.Message, linkError.Value.Code, null);

        _passwordService.CreateHash(
            request.Password,
            out var passwordHash,
            out var passwordSalt);

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            // Chỉ Admin gọi được endpoint này (xem UsersController) nên được chọn role
            Role = role,
            Department = "General",
            Age = request.Age,
            EmployeeId = request.EmployeeId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return (true, "Đăng ký thành công.", null, MapToResponseDto(user));
    }

    public async Task<(
        bool IsSuccess,
        string Message,
        string? ErrorCode,
        UserResponseDto? Data)> UpdateAsync(int id, UserUpdateDto request)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user is null)
        {
            return (
                false,
                $"Không tìm thấy người dùng có Id = {id}",
                "USER_NOT_FOUND",
                null);
        }

        var username = request.Username?.Trim();
        var email = request.Email?.Trim();

        var duplicateExists = await _userRepository
            .IsUsernameOrEmailUsedByAnotherAsync(id, username, email);

        if (duplicateExists)
        {
            return (
                false,
                "Username hoặc Email đã được sử dụng.",
                "USER_ALREADY_EXISTS",
                null);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            user.Username = username;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            user.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = request.Role.Trim();
            if (!UserRoles.All.Contains(role))
            {
                return (
                    false,
                    "Role chỉ được phép là Admin, Manager hoặc User.",
                    "INVALID_ROLE",
                    null);
            }

            user.Role = UserRoles.All.First(item =>
                item.Equals(role, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Department))
            user.Department = request.Department.Trim().ToUpperInvariant();

        if (request.Age.HasValue)
            user.Age = request.Age.Value;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            _passwordService.CreateHash(
                request.Password,
                out var passwordHash,
                out var passwordSalt);
            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
        }

        if (request.EmployeeId.HasValue && request.EmployeeId != user.EmployeeId)
        {
            var linkError = await ValidateEmployeeLinkAsync(request.EmployeeId, id);
            if (linkError is not null)
                return (false, linkError.Value.Message, linkError.Value.Code, null);
            user.EmployeeId = request.EmployeeId;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
            if (!user.IsActive)
            {
                // Khóa tài khoản thì thu hồi luôn refresh token
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiresAt = null;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return (
            true,
            $"Cập nhật thành công người dùng Id = {id}",
            null,
            MapToResponseDto(user));
    }

    public async Task<(bool IsSuccess, string Message, string? ErrorCode)>
        DeleteAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user is null)
        {
            return (
                false,
                $"Không tìm thấy người dùng có Id = {id}",
                "USER_NOT_FOUND");
        }

        _userRepository.Delete(user);
        await _userRepository.SaveChangesAsync();

        return (true, $"Đã xóa người dùng Id = {id}", null);
    }

    private static UserResponseDto MapToResponseDto(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            Department = user.Department,
            Age = user.Age,
            IsActive = user.IsActive,
            EmployeeId = user.EmployeeId,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private async Task<(string Message, string Code)?> ValidateEmployeeLinkAsync(int? employeeId, int? exceptUserId)
    {
        if (employeeId is not int value) return null;
        if (!await _userRepository.EmployeeExistsAsync(value))
            return ($"Không tìm thấy nhân viên Id = {value}.", "EMPLOYEE_NOT_FOUND");
        if (await _userRepository.IsEmployeeLinkedAsync(value, exceptUserId))
            return ("Nhân viên này đã có tài khoản.", "EMPLOYEE_ALREADY_LINKED");
        return null;
    }
}
