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
    private readonly IAuthRepository _authRepository;

    public UserService(IUserRepository userRepository, IPasswordService passwordService, IAuthRepository authRepository)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _authRepository = authRepository;
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

    public async Task<ProfileResponseDto?> GetProfileAsync(int userId)
    {
        var user = await _userRepository.GetWithEmployeeAsync(userId);
        return user is null ? null : new ProfileResponseDto
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            EmployeeId = user.EmployeeId,
            EmployeeCode = user.Employee?.EmployeeCode,
            FullName = user.Employee?.FullName,
            PhoneNumber = user.Employee?.PhoneNumber
        };
    }

    public async Task<(bool IsSuccess, string Message, string? ErrorCode)> ChangePasswordAsync(int userId, ChangePasswordDto request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null || !user.IsActive) return (false, "Không tìm thấy tài khoản.", "USER_NOT_FOUND");
        if (!_passwordService.Verify(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            return (false, "Mật khẩu hiện tại không đúng.", "WRONG_PASSWORD");
        if (request.NewPassword == request.CurrentPassword)
            return (false, "Mật khẩu mới phải khác mật khẩu hiện tại.", "SAME_PASSWORD");

        _passwordService.CreateHash(request.NewPassword, out var hash, out var salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();
        // Mật khẩu đổi → mọi JWT/refresh token cũ hết hiệu lực (giống bản sửa P1-02)
        await _authRepository.RevokeSessionsAsync(userId);
        return (true, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.", null);
    }

    public async Task<(bool IsSuccess, string Message, string? ErrorCode)> UpdatePhoneAsync(int userId, UpdatePhoneDto request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null || !user.IsActive) return (false, "Không tìm thấy tài khoản.", "USER_NOT_FOUND");
        if (user.EmployeeId is not int employeeId)
            return (false, "Tài khoản chưa gắn với nhân viên nên chưa có số điện thoại để cập nhật.", "ACCOUNT_NOT_LINKED");

        var raw = new string((request.PhoneNumber ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-').ToArray());
        if (raw.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(raw, @"^(\+84|0)[0-9]{9,10}$"))
            return (false, "Số điện thoại không hợp lệ (vd: 0901234567 hoặc +84901234567).", "INVALID_PHONE");

        await _userRepository.UpdateEmployeePhoneAsync(employeeId, raw.Length == 0 ? null : raw);
        return (true, raw.Length == 0 ? "Đã xóa số điện thoại." : "Đã cập nhật số điện thoại.", null);
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
        // Thay đổi ảnh hưởng tới xác thực/phân quyền → phải thu hồi các phiên (JWT, refresh token) cũ
        var revokeSessions = false;

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

            var newRole = UserRoles.All.First(item =>
                item.Equals(role, StringComparison.OrdinalIgnoreCase));
            revokeSessions |= newRole != user.Role;
            user.Role = newRole;
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
            revokeSessions = true;
        }

        if (request.EmployeeId.HasValue && request.EmployeeId != user.EmployeeId)
        {
            var linkError = await ValidateEmployeeLinkAsync(request.EmployeeId, id);
            if (linkError is not null)
                return (false, linkError.Value.Message, linkError.Value.Code, null);
            user.EmployeeId = request.EmployeeId;
            revokeSessions = true;
        }

        if (request.IsActive.HasValue)
        {
            revokeSessions |= user.IsActive && !request.IsActive.Value; // khóa tài khoản
            user.IsActive = request.IsActive.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        // Entity đang được tracking: SaveChanges chỉ ghi các cột đã đổi. Không gọi DbSet.Update (đánh dấu
        // MỌI cột) vì sẽ ghi đè TokenVersion/RefreshTokenHash cũ nếu logout/refresh chạy xen giữa.
        await _userRepository.SaveChangesAsync();
        if (revokeSessions)
            await _authRepository.RevokeSessionsAsync(id);

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
