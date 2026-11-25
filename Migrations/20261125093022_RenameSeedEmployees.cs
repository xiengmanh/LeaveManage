using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManage.Migrations
{
    /// <inheritdoc />
    public partial class RenameSeedEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 1,
                column: "FullName",
                value: "Nguyễn Hoàng Long");

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 2,
                column: "FullName",
                value: "Trần Thu Hà");

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 5,
                column: "FullName",
                value: "Hoàng Thu Hương");

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 6,
                column: "FullName",
                value: "Đỗ Ngọc Lan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 1,
                column: "FullName",
                value: "Nguyễn Văn Giám Đốc");

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 2,
                column: "FullName",
                value: "Trần Thị Quản Lý");

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 5,
                column: "FullName",
                value: "Hoàng Thị Hương");

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: 6,
                column: "FullName",
                value: "Đỗ Thị Lan");
        }
    }
}
