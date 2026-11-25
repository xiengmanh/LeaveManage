using System.Security.Claims;
using LeaveManage.Constants;
using LeaveManage.DTOs.Common;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiErrorResponse = LeaveManage.DTOs.Responses.ErrorResponse;

namespace LeaveManage.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;

    public UsersController(IUserService userService, IAuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    /// <summary>Lấy danh sách tài khoản.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<PagedResult<UserResponseDto>>> GetAll(
        [FromQuery] UserQueryParameters queryParams)
    {
        var result = await _userService.GetAllAsync(queryParams);
        return Ok(result);
    }

    /// <summary>Lấy tài khoản theo ID.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<UserResponseDto>> GetById(int id)
    {
        var user = await _userService.GetByIdAsync(id);

        if (user is null)
        {
            return NotFound(CreateError(
                $"Không tìm thấy người dùng có Id = {id}",
                "USER_NOT_FOUND"));
        }

        return Ok(user);
    }

    /// <summary>Admin tạo tài khoản mới (chọn role, gắn nhân viên). Không cho tự đăng ký công khai.</summary>
    [HttpPost("register")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<UserResponseDto>> Register(
        [FromBody] UserRegisterDto request)
    {
        var result = await _userService.RegisterAsync(request);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Message, result.ErrorCode);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Data!.Id },
            result.Data);
    }

    /// <summary>Kiểm tra thông tin đăng nhập.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] UserLoginDto request)
    {
        var tokens = await _authService.LoginAsync(request);

        if (tokens is null)
        {
            return Unauthorized(CreateError(
                "Sai tên đăng nhập hoặc mật khẩu, hoặc tài khoản đã bị khóa.",
                "INVALID_CREDENTIALS"));
        }

        return Ok(tokens);
    }

    /// <summary>Đổi Refresh Token hợp lệ lấy một cặp token mới.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        var tokens = await _authService.RefreshTokenAsync(request.RefreshToken);
        return tokens is null
            ? Unauthorized(CreateError("Refresh Token không hợp lệ hoặc đã hết hạn.", "INVALID_REFRESH_TOKEN"))
            : Ok(tokens);
    }

    /// <summary>Đăng xuất: thu hồi refresh token của tài khoản hiện tại.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            await _authService.LogoutAsync(userId);
        return NoContent();
    }

    /// <summary>Đọc ngược thông tin người dùng từ JWT.</summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Unknown";
        var department = User.FindFirstValue(CustomClaimTypes.Department) ?? "Unknown";
        var age = User.FindFirstValue(CustomClaimTypes.Age) ?? "Unknown";
        var employeeId = int.TryParse(User.FindFirstValue(CustomClaimTypes.EmployeeId), out var eid) ? eid : (int?)null;

        return Ok(new
        {
            Message = $"Xin chào {username}, bạn đang ở nhóm {role}",
            Username = username,
            Role = role,
            Department = department,
            Age = age,
            EmployeeId = employeeId
        });
    }

    /// <summary>Endpoint minh họa Custom MinimumAgeRequirement.</summary>
    [HttpGet("adult-area")]
    [Authorize(Policy = AuthorizationPolicies.MinimumAge18)]
    public IActionResult AdultArea()
    {
        return Ok(new { Message = "Bạn thỏa mãn policy từ 18 tuổi trở lên." });
    }

    /// <summary>Cập nhật tài khoản.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UserUpdateDto request)
    {
        var result = await _userService.UpdateAsync(id, request);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Message, result.ErrorCode);
        }

        return Ok(SuccessResponse.Ok(result.Message, result.Data));
    }

    /// <summary>Xóa tài khoản.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _userService.DeleteAsync(id);

        if (!result.IsSuccess)
        {
            return NotFound(CreateError(result.Message, result.ErrorCode));
        }

        return Ok(SuccessResponse.Ok(result.Message));
    }

    private ObjectResult MapFailure(string message, string? errorCode)
    {
        var error = CreateError(message, errorCode);
        return errorCode switch
        {
            "USER_NOT_FOUND" or "EMPLOYEE_NOT_FOUND" => NotFound(error),
            "USER_ALREADY_EXISTS" or "EMPLOYEE_ALREADY_LINKED" => Conflict(error),
            _ => BadRequest(error)
        };
    }

    private static ApiErrorResponse CreateError(
        string message,
        string? errorCode)
    {
        return new ApiErrorResponse
        {
            Message = message,
            ErrorCode = errorCode
        };
    }
}
