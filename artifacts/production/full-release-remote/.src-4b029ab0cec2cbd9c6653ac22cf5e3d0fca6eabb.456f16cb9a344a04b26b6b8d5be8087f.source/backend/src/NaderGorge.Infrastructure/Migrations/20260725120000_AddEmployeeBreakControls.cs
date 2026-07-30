using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260725120000_AddEmployeeBreakControls")]
public partial class AddEmployeeBreakControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "DailyBreakAllowanceMinutes", table: "employee_profiles", type: "integer", nullable: false, defaultValue: 30);
        migrationBuilder.AddColumn<int>(name: "ShortPermissionMaxMinutes", table: "employee_profiles", type: "integer", nullable: false, defaultValue: 5);
        migrationBuilder.AddColumn<int>(name: "DailyShortPermissionAllowanceMinutes", table: "employee_profiles", type: "integer", nullable: false, defaultValue: 15);
        migrationBuilder.AddColumn<int>(name: "Kind", table: "hr_attendance_breaks", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "AllowedMinutes", table: "hr_attendance_breaks", type: "integer", nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DailyBreakAllowanceMinutes", table: "employee_profiles");
        migrationBuilder.DropColumn(name: "ShortPermissionMaxMinutes", table: "employee_profiles");
        migrationBuilder.DropColumn(name: "DailyShortPermissionAllowanceMinutes", table: "employee_profiles");
        migrationBuilder.DropColumn(name: "Kind", table: "hr_attendance_breaks");
        migrationBuilder.DropColumn(name: "AllowedMinutes", table: "hr_attendance_breaks");
    }
}
