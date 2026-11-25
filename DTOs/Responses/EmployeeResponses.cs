namespace LeaveManage.DTOs.Responses;

// ==================== EMPLOYEE RESPONSE DTOs ====================

/// <summary>
/// Response cơ bản cho Employee
/// </summary>
public class EmployeeResponse
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Thông tin liên quan
    public DepartmentResponse? Department { get; set; }
    public PositionResponse? Position { get; set; }
    public EmployeeBasicResponse? Manager { get; set; }
}

/// <summary>
/// Response cơ bản cho Department
/// </summary>
public class DepartmentResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class DepartmentEmployeesResponse
{
    public DepartmentResponse Department { get; set; } = new();
    public List<EmployeeBasicResponse> Employees { get; set; } = new();
}

/// <summary>
/// Response cơ bản cho Position
/// </summary>
public class PositionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Response cơ bản cho Employee (dùng trong dropdown)
/// </summary>
public class EmployeeBasicResponse
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Response phân trang chung
/// </summary>
public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Response thành công
/// </summary>
public class SuccessResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    
    public static SuccessResponse Ok(string message = "Thành công", object? data = null)
    {
        return new SuccessResponse 
        { 
            Success = true, 
            Message = message, 
            Data = data 
        };
    }
}

/// <summary>
/// Response lỗi
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    
    public static ErrorResponse Fail(string message, string? errorCode = null)
    {
        return new ErrorResponse
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode
        };
    }
}
