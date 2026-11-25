using LeaveManage.Constants;
using LeaveManage.Models;
using LeaveManage.Services;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Data;

/// <summary>
/// Tạo dữ liệu demo lúc khởi động (chỉ khi Seed:Enabled = true, mặc định chỉ bật ở Development).
/// Idempotent: chạy nhiều lần không tạo trùng.
/// Mật khẩu demo lấy từ cấu hình Seed:DemoPassword (user-secrets), không ghi trong mã nguồn.
/// Danh mục, 6 nhân viên và số dư 2026 đã có sẵn trong migration (HasData).
/// </summary>
public static class DbSeeder
{
    private static readonly (string Username, string Role, int EmployeeId, string Department)[] DemoAccounts =
    {
        ("admin", UserRoles.Admin, 1, "HR"),
        ("manager", UserRoles.Manager, 2, "IT"),
        ("nv003", UserRoles.User, 3, "IT"),
        ("nv004", UserRoles.User, 4, "IT"),
        ("nv005", UserRoles.User, 5, "SALES"),
        ("nv006", UserRoles.User, 6, "IT"),
    };

    public static async Task SeedAsync(AppDbContext db, IPasswordService passwordService, IConfiguration configuration, ILogger logger)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DemoPassword chưa được cấu hình (dotnet user-secrets) → bỏ qua tạo tài khoản demo.");
        }
        else
        {
            foreach (var (username, role, employeeId, department) in DemoAccounts)
            {
                if (await db.Users.AnyAsync(u => u.Username == username || u.EmployeeId == employeeId))
                    continue;
                var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
                if (employee is null) continue;

                passwordService.CreateHash(password, out var hash, out var salt);
                db.Users.Add(new User
                {
                    Username = username,
                    Email = employee.Email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Role = role,
                    Department = department,
                    Age = Math.Clamp(DateTime.Today.Year - employee.DateOfBirth.Year, 18, 100),
                    EmployeeId = employeeId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        // Bảo đảm năm hiện tại có số dư phép năm + phép ốm cho nhân viên đang làm (nếu demo sang năm mới)
        var year = DateTime.Today.Year;
        var deductibleTypes = await db.LeaveTypes.Where(t => t.AllowsDeduction && t.IsActive && t.DefaultDaysPerYear != null).ToListAsync();
        var employeeIds = await db.Employees.Where(e => e.IsActive).Select(e => e.Id).ToListAsync();
        foreach (var employeeId in employeeIds)
        {
            foreach (var type in deductibleTypes)
            {
                if (await db.LeaveBalances.AnyAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == type.Id && b.Year == year))
                    continue;
                db.LeaveBalances.Add(new LeaveBalance
                {
                    EmployeeId = employeeId,
                    LeaveTypeId = type.Id,
                    Year = year,
                    InitialDays = type.DefaultDaysPerYear!.Value,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
