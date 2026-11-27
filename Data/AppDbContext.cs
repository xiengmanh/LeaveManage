using Microsoft.EntityFrameworkCore;
using LeaveManage.Models;

namespace LeaveManage.Data;

/// <summary>
/// DbContext - Định nghĩa cách Models ánh xạ xuống Database Tables.
/// Chỉ tầng Repositories/Data dùng lớp này; Controller và Service không truy cập trực tiếp.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ===== DbSets =====
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveApproval> LeaveApprovals => Set<LeaveApproval>();
    public DbSet<User> Users => Set<User>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Holiday> Holidays => Set<Holiday>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== EMPLOYEE RELATIONSHIPS =====
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.EmployeeCode).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();

            // Self-referencing relationship (Manager): một quản lý có nhiều nhân viên cấp dưới
            entity.HasOne(e => e.Manager)
                  .WithMany()
                  .HasForeignKey(e => e.ManagerId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Department)
                  .WithMany(d => d.Employees)
                  .HasForeignKey(e => e.DepartmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Position)
                  .WithMany(p => p.Employees)
                  .HasForeignKey(e => e.PositionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Department>(entity => entity.HasIndex(d => d.Code).IsUnique());
        modelBuilder.Entity<Position>(entity => entity.HasIndex(p => p.Code).IsUnique());
        modelBuilder.Entity<LeaveType>(entity => entity.HasIndex(t => t.Code).IsUnique());

        // ===== USER ACCOUNT: 1 nhân viên có tối đa 1 tài khoản =====
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.EmployeeId).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
            entity.HasOne(user => user.Employee)
                  .WithMany()
                  .HasForeignKey(user => user.EmployeeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasIndex(h => h.Date).IsUnique();
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasIndex(log => log.CreatedAt);
            entity.HasIndex(log => log.UserId);
            entity.HasIndex(log => log.Username);
        });

        // ===== LEAVE REQUEST =====
        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasIndex(r => r.RequestNumber).IsUnique();
            entity.HasIndex(r => new { r.EmployeeId, r.Status });
            entity.ToTable(t => t.HasCheckConstraint("CK_LeaveRequests_Dates", "[EndDate] >= [StartDate]"));
            entity.HasOne(r => r.Employee)
                  .WithMany(e => e.LeaveRequests)
                  .HasForeignKey(r => r.EmployeeId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.LeaveType)
                  .WithMany(t => t.LeaveRequests)
                  .HasForeignKey(r => r.LeaveTypeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== LEAVE BALANCE: mỗi nhân viên có 1 dòng / loại nghỉ / năm =====
        modelBuilder.Entity<LeaveBalance>(entity =>
        {
            entity.HasIndex(lb => new { lb.EmployeeId, lb.LeaveTypeId, lb.Year }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_LeaveBalances_NonNegative", "[UsedDays] >= 0 AND [PendingDays] >= 0 AND [InitialDays] >= 0"));
            entity.HasOne(lb => lb.Employee)
                  .WithMany(e => e.LeaveBalances)
                  .HasForeignKey(lb => lb.EmployeeId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(lb => lb.LeaveType)
                  .WithMany(t => t.LeaveBalances)
                  .HasForeignKey(lb => lb.LeaveTypeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== LEAVE APPROVAL: lịch sử xét duyệt; mỗi đơn chỉ được xét duyệt 1 lần (chốt chặn ở database) =====
        modelBuilder.Entity<LeaveApproval>(entity =>
        {
            entity.HasIndex(la => la.LeaveRequestId).IsUnique();
            entity.HasOne(la => la.LeaveRequest)
                  .WithMany(lr => lr.Approvals)
                  .HasForeignKey(la => la.LeaveRequestId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(la => la.Approver)
                  .WithMany(e => e.Approvals)
                  .HasForeignKey(la => la.ApproverId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // SEED DATA (dữ liệu danh mục cố định). Tài khoản đăng nhập được tạo lúc chạy bởi DbSeeder
        // để băm mật khẩu bằng cùng thuật toán với đăng nhập.
        // ==========================================
        var seedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Công nghệ thông tin", Code = "IT", Description = "Phòng CNTT", CreatedAt = seedTime },
            new Department { Id = 2, Name = "Nhân sự", Code = "HR", Description = "Phòng Nhân sự", CreatedAt = seedTime },
            new Department { Id = 3, Name = "Kinh doanh", Code = "SALES", Description = "Phòng Kinh doanh", CreatedAt = seedTime },
            new Department { Id = 4, Name = "Tài chính - Kế toán", Code = "FIN", Description = "Phòng Tài chính", CreatedAt = seedTime }
        );

        modelBuilder.Entity<Position>().HasData(
            new Position { Id = 1, Name = "Thực tập sinh", Code = "INTERN", Level = 1, CreatedAt = seedTime },
            new Position { Id = 2, Name = "Nhân viên", Code = "JUNIOR", Level = 2, CreatedAt = seedTime },
            new Position { Id = 3, Name = "Kỹ sư cao cấp", Code = "SENIOR", Level = 3, CreatedAt = seedTime },
            new Position { Id = 4, Name = "Trưởng nhóm", Code = "LEAD", Level = 4, CreatedAt = seedTime },
            new Position { Id = 5, Name = "Quản lý", Code = "MANAGER", Level = 5, CreatedAt = seedTime },
            new Position { Id = 6, Name = "Giám đốc", Code = "DIRECTOR", Level = 6, CreatedAt = seedTime }
        );

        modelBuilder.Entity<LeaveType>().HasData(
            new LeaveType { Id = 1, Name = "Nghỉ phép năm", Code = "ANNUAL", DefaultDaysPerYear = 12, RequiresApproval = true, NoticeDaysRequired = 3, AllowsDeduction = true, CreatedAt = seedTime },
            new LeaveType { Id = 2, Name = "Nghỉ ốm", Code = "SICK", DefaultDaysPerYear = 30, RequiresApproval = true, NoticeDaysRequired = 0, AllowsDeduction = true, CreatedAt = seedTime },
            new LeaveType { Id = 3, Name = "Nghỉ thai sản", Code = "MATERNITY", DefaultDaysPerYear = 180, RequiresApproval = true, NoticeDaysRequired = 30, AllowsDeduction = false, CreatedAt = seedTime },
            new LeaveType { Id = 4, Name = "Nghỉ không lương", Code = "UNPAID", DefaultDaysPerYear = null, RequiresApproval = true, NoticeDaysRequired = 7, AllowsDeduction = false, CreatedAt = seedTime },
            new LeaveType { Id = 5, Name = "Nghỉ cưới", Code = "WEDDING", DefaultDaysPerYear = 3, RequiresApproval = true, NoticeDaysRequired = 0, AllowsDeduction = false, CreatedAt = seedTime }
        );

        // Nhân viên mẫu (giữ từ LTW22, bỏ mật khẩu). Quản lý trực tiếp: NV002 quản lý NV003, NV004, NV006.
        modelBuilder.Entity<Employee>().HasData(
            new Employee { Id = 1, EmployeeCode = "NV001", FullName = "Nguyễn Hoàng Long", Email = "giamdoc@company.com", PhoneNumber = "0901234567", DateOfBirth = new DateTime(1980, 1, 1), HireDate = new DateTime(2015, 1, 1), DepartmentId = 1, PositionId = 6, ManagerId = null, IsActive = true, CreatedAt = seedTime },
            new Employee { Id = 2, EmployeeCode = "NV002", FullName = "Trần Thu Hà", Email = "quanly.it@company.com", PhoneNumber = "0901234568", DateOfBirth = new DateTime(1985, 5, 15), HireDate = new DateTime(2018, 3, 1), DepartmentId = 1, PositionId = 5, ManagerId = 1, IsActive = true, CreatedAt = seedTime },
            new Employee { Id = 3, EmployeeCode = "NV003", FullName = "Lê Hoàng Nam", Email = "nam.senior@company.com", PhoneNumber = "0901234569", DateOfBirth = new DateTime(1990, 8, 20), HireDate = new DateTime(2019, 6, 15), DepartmentId = 1, PositionId = 3, ManagerId = 2, IsActive = true, CreatedAt = seedTime },
            new Employee { Id = 4, EmployeeCode = "NV004", FullName = "Phạm Minh Tuấn", Email = "tuan.junior@company.com", PhoneNumber = "0901234570", DateOfBirth = new DateTime(1995, 12, 10), HireDate = new DateTime(2023, 1, 10), DepartmentId = 1, PositionId = 2, ManagerId = 2, IsActive = true, CreatedAt = seedTime },
            new Employee { Id = 5, EmployeeCode = "NV005", FullName = "Hoàng Thu Hương", Email = "hr@company.com", PhoneNumber = "0901234571", DateOfBirth = new DateTime(1992, 3, 25), HireDate = new DateTime(2020, 2, 1), DepartmentId = 2, PositionId = 3, ManagerId = 1, IsActive = true, CreatedAt = seedTime },
            new Employee { Id = 6, EmployeeCode = "NV006", FullName = "Đỗ Ngọc Lan", Email = "lan.intern@company.com", PhoneNumber = "0901234572", DateOfBirth = new DateTime(2001, 7, 30), HireDate = new DateTime(2024, 6, 1), DepartmentId = 1, PositionId = 1, ManagerId = 2, IsActive = true, CreatedAt = seedTime }
        );

        // Số dư phép năm 2026: phép năm 12 ngày, nghỉ ốm 30 ngày cho mỗi nhân viên
        for (var empId = 1; empId <= 6; empId++)
        {
            modelBuilder.Entity<LeaveBalance>().HasData(
                new LeaveBalance { Id = empId * 10 + 1, EmployeeId = empId, LeaveTypeId = 1, Year = 2026, InitialDays = 12, UsedDays = 0, PendingDays = 0, CarriedForwardDays = 0, MaxCarryForwardDays = 5, CreatedAt = seedTime },
                new LeaveBalance { Id = empId * 10 + 2, EmployeeId = empId, LeaveTypeId = 2, Year = 2026, InitialDays = 30, UsedDays = 0, PendingDays = 0, CarriedForwardDays = 0, MaxCarryForwardDays = 0, CreatedAt = seedTime });
        }
    }
}
