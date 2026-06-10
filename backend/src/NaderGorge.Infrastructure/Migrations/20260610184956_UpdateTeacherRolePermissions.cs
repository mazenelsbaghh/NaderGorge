using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTeacherRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[\"content.manage\", \"exams.manage\", \"comments.manage\"]' WHERE \"Name\" = 'Teacher';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[]' WHERE \"Name\" = 'Teacher';");
        }
    }
}
