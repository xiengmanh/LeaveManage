using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LeaveManage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DefaultDaysPerYear = table.Column<int>(type: "int", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    NoticeDaysRequired = table.Column<int>(type: "int", nullable: true),
                    AllowsDeduction = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: false),
                    ManagerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    InitialDays = table.Column<int>(type: "int", nullable: false),
                    UsedDays = table.Column<int>(type: "int", nullable: false),
                    PendingDays = table.Column<int>(type: "int", nullable: false),
                    CarriedForwardDays = table.Column<int>(type: "int", nullable: false),
                    MaxCarryForwardDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                    table.CheckConstraint("CK_LeaveBalances_NonNegative", "[UsedDays] >= 0 AND [PendingDays] >= 0 AND [InitialDays] >= 0");
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalDays = table.Column<int>(type: "int", nullable: false),
                    PaidDays = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.CheckConstraint("CK_LeaveRequests_Dates", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApprovalLevel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaveRequestId = table.Column<int>(type: "int", nullable: false),
                    ApproverId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveApprovals_Employees_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveApprovals_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "IT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Phòng CNTT", true, "Công nghệ thông tin" },
                    { 2, "HR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Phòng Nhân sự", true, "Nhân sự" },
                    { 3, "SALES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Phòng Kinh doanh", true, "Kinh doanh" },
                    { 4, "FIN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Phòng Tài chính", true, "Tài chính - Kế toán" }
                });

            migrationBuilder.InsertData(
                table: "LeaveTypes",
                columns: new[] { "Id", "AllowsDeduction", "Code", "CreatedAt", "DefaultDaysPerYear", "Description", "IsActive", "Name", "NoticeDaysRequired", "RequiresApproval" },
                values: new object[,]
                {
                    { 1, true, "ANNUAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 12, null, true, "Nghỉ phép năm", 3, true },
                    { 2, true, "SICK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 30, null, true, "Nghỉ ốm", 0, true },
                    { 3, false, "MATERNITY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 180, null, true, "Nghỉ thai sản", 30, true },
                    { 4, false, "UNPAID", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Nghỉ không lương", 7, true },
                    { 5, false, "WEDDING", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, null, true, "Nghỉ cưới", 0, true }
                });

            migrationBuilder.InsertData(
                table: "Positions",
                columns: new[] { "Id", "Code", "CreatedAt", "IsActive", "Level", "Name" },
                values: new object[,]
                {
                    { 1, "INTERN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1, "Thực tập sinh" },
                    { 2, "JUNIOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 2, "Nhân viên" },
                    { 3, "SENIOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "Kỹ sư cao cấp" },
                    { 4, "LEAD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 4, "Trưởng nhóm" },
                    { 5, "MANAGER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 5, "Quản lý" },
                    { 6, "DIRECTOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 6, "Giám đốc" }
                });

            migrationBuilder.InsertData(
                table: "Employees",
                columns: new[] { "Id", "CreatedAt", "DateOfBirth", "DepartmentId", "Email", "EmployeeCode", "FullName", "HireDate", "IsActive", "ManagerId", "PhoneNumber", "PositionId", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1980, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "giamdoc@company.com", "NV001", "Nguyễn Văn Giám Đốc", new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "0901234567", 6, null },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1985, 5, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "quanly.it@company.com", "NV002", "Trần Thị Quản Lý", new DateTime(2018, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 1, "0901234568", 5, null },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1992, 3, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "hr@company.com", "NV005", "Hoàng Thị Hương", new DateTime(2020, 2, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 1, "0901234571", 3, null }
                });

            migrationBuilder.InsertData(
                table: "LeaveBalances",
                columns: new[] { "Id", "CarriedForwardDays", "CreatedAt", "EmployeeId", "InitialDays", "LeaveTypeId", "MaxCarryForwardDays", "PendingDays", "UpdatedAt", "UsedDays", "Year" },
                values: new object[,]
                {
                    { 11, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 12, 1, 5, 0, null, 0, 2026 },
                    { 12, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 30, 2, 0, 0, null, 0, 2026 }
                });

            migrationBuilder.InsertData(
                table: "Employees",
                columns: new[] { "Id", "CreatedAt", "DateOfBirth", "DepartmentId", "Email", "EmployeeCode", "FullName", "HireDate", "IsActive", "ManagerId", "PhoneNumber", "PositionId", "UpdatedAt" },
                values: new object[,]
                {
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1990, 8, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "nam.senior@company.com", "NV003", "Lê Hoàng Nam", new DateTime(2019, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 2, "0901234569", 3, null },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1995, 12, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "tuan.junior@company.com", "NV004", "Phạm Minh Tuấn", new DateTime(2023, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 2, "0901234570", 2, null },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2001, 7, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "lan.intern@company.com", "NV006", "Đỗ Thị Lan", new DateTime(2024, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 2, "0901234572", 1, null }
                });

            migrationBuilder.InsertData(
                table: "LeaveBalances",
                columns: new[] { "Id", "CarriedForwardDays", "CreatedAt", "EmployeeId", "InitialDays", "LeaveTypeId", "MaxCarryForwardDays", "PendingDays", "UpdatedAt", "UsedDays", "Year" },
                values: new object[,]
                {
                    { 21, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, 12, 1, 5, 0, null, 0, 2026 },
                    { 22, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, 30, 2, 0, 0, null, 0, 2026 },
                    { 51, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, 12, 1, 5, 0, null, 0, 2026 },
                    { 52, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, 30, 2, 0, 0, null, 0, 2026 },
                    { 31, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, 12, 1, 5, 0, null, 0, 2026 },
                    { 32, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, 30, 2, 0, 0, null, 0, 2026 },
                    { 41, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, 12, 1, 5, 0, null, 0, 2026 },
                    { 42, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, 30, 2, 0, 0, null, 0, 2026 },
                    { 61, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 6, 12, 1, 5, 0, null, 0, 2026 },
                    { 62, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 6, 30, 2, 0, 0, null, 0, 2026 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PositionId",
                table: "Employees",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApprovals_ApproverId",
                table: "LeaveApprovals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApprovals_LeaveRequestId",
                table: "LeaveApprovals",
                column: "LeaveRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeId_LeaveTypeId_Year",
                table: "LeaveBalances",
                columns: new[] { "EmployeeId", "LeaveTypeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_LeaveTypeId",
                table: "LeaveBalances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_Status",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_LeaveTypeId",
                table: "LeaveRequests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_RequestNumber",
                table: "LeaveRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_Code",
                table: "LeaveTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Code",
                table: "Positions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "[EmployeeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveApprovals");

            migrationBuilder.DropTable(
                name: "LeaveBalances");

            migrationBuilder.DropTable(
                name: "StoredFiles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "LeaveTypes");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Positions");
        }
    }
}
