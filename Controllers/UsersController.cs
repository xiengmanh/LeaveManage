using System.Security.Claims;
using LeaveManage.Authentication;
using LeaveManage.Constants;
using LeaveManage.DTOs.Common;
using LeaveManage.DTOs.Requests;
using LeaveManage.DTOs.Responses;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApiErrorResponse = LeaveManage.DTOs.Responses.ErrorResponse;

namespace LeaveManage.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IActivityLogService _activityLog;

    public UsersController(IUserService userService, IAuthService authService, IActivityLogService activityLog)
    {
        _userService = userService;
        _authService = authService;
        _activityLog = activityLog;
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
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login([FromBody] UserLoginDto request)
    {
        var tokens = await _authService.LoginAsync(request);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (tokens is null)
        {
            await _activityLog.LogAsync(null, request.Username.Trim(), "Đăng nhập thất bại", "POST", Request.Path, StatusCodes.Status401Unauthorized, ip);
            return Unauthorized(CreateError(
                "Sai tên đăng nhập hoặc mật khẩu, hoặc tài khoản đã bị khóa.",
                "INVALID_CREDENTIALS"));
        }

        var subject = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokens.Token)
            .Claims.FirstOrDefault(c => c.Type is ClaimTypes.NameIdentifier or "nameid")?.Value;
        await _activityLog.LogAsync(int.TryParse(subject, out var uid) ? uid : null, tokens.Username, "Đăng nhập", "POST", Request.Path, StatusCodes.Status200OK, ip);
        RefreshTokenCookie.Append(Response, tokens);
        return Ok(tokens);
    }

    /// <summary>Đổi Refresh Token (đọc từ cookie HttpOnly lm_refresh) lấy một cặp token mới.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie.Name];
        var tokens = string.IsNullOrEmpty(refreshToken)
            ? null
            : await _authService.RefreshTokenAsync(refreshToken);

        if (tokens is null)
        {
            RefreshTokenCookie.Delete(Response);
            return Unauthorized(CreateError("Refresh Token không hợp lệ hoặc đã hết hạn.", "INVALID_REFRESH_TOKEN"));
        }

        RefreshTokenCookie.Append(Response, tokens);
        return Ok(tokens);
    }

    /// <summary>Đăng xuất: thu hồi refresh token của tài khoản hiện tại và xóa cookie.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            await _authService.LogoutAsync(userId);
        RefreshTokenCookie.Delete(Response);
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

    /// <summary>Thông tin tài khoản và nhân viên của chính mình.</summary>
    [HttpGet("me/profile")]
    public async Task<IActionResult> Profile()
    {
        var profile = await _userService.GetProfileAsync(CallerContext.From(User).UserId);
        return profile is null ? NotFound(CreateError("Không tìm thấy tài khoản.", "USER_NOT_FOUND")) : Ok(profile);
    }

    /// <summary>Đổi mật khẩu của chính mình (mọi vai trò). Các phiên đăng nhập cũ bị thu hồi.</summary>
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
    {
        var result = await _userService.ChangePasswordAsync(CallerContext.From(User).UserId, request);
        return result.IsSuccess ? Ok(SuccessResponse.Ok(result.Message)) : MapFailure(result.Message, result.ErrorCode);
    }

    /// <summary>Đổi số điện thoại của chính mình (mọi vai trò có hồ sơ nhân viên).</summary>
    [HttpPut("me/phone")]
    public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneDto request)
    {
        var result = await _userService.UpdatePhoneAsync(CallerContext.From(User).UserId, request);
        return result.IsSuccess ? Ok(SuccessResponse.Ok(result.Message)) : MapFailure(result.Message, result.ErrorCode);
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
        // Không cho Admin tự khóa hoặc tự hạ quyền chính mình (tránh mất quyền quản trị)
        if (IsCurrentUser(id) && (request.IsActive == false
            || (request.Role is not null && !request.Role.Equals(UserRoles.Admin, StringComparison.OrdinalIgnoreCase))))
        {
            return BadRequest(CreateError("Không thể tự khóa hoặc tự hạ quyền tài khoản đang đăng nhập.", "CANNOT_CHANGE_SELF"));
        }

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
        if (IsCurrentUser(id))
            return BadRequest(CreateError("Không thể tự xóa tài khoản đang đăng nhập.", "CANNOT_CHANGE_SELF"));

        var result = await _userService.DeleteAsync(id);

        if (!result.IsSuccess)
        {
            return NotFound(CreateError(result.Message, result.ErrorCode));
        }

        return Ok(SuccessResponse.Ok(result.Message));
    }

    private bool IsCurrentUser(int id) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentId) && currentId == id;

    private ObjectResult MapFailure(string message, string? errorCode)
    {
        var error = CreateError(message, errorCode);
        return errorCode switch
        {
            "USER_NOT_FOUND" or "EMPLOYEE_NOT_FOUND" => NotFound(error),
            "ACCOUNT_NOT_LINKED" => StatusCode(StatusCodes.Status403Forbidden, error),
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
