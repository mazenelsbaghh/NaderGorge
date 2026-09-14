using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    public partial class EnforceSingleTeacherStaffMembership : Migration
    {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_teacher_staff_members_UserId",
            table: "teacher_staff_members");

        migrationBuilder.CreateIndex(
            name: "IX_teacher_staff_members_UserId",
            table: "teacher_staff_members",
            column: "UserId",
            unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_teacher_staff_members_UserId",
                table: "teacher_staff_members");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_staff_members_UserId",
                table: "teacher_staff_members",
                column: "UserId");
        }
    }
}
