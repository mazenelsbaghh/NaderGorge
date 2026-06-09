using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDefaultRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[\"users.manage\", \"content.manage\", \"exams.manage\", \"codes.manage\", \"watch_requests.manage\", \"community.manage\", \"comments.manage\", \"hr.manage\", \"tasks.manage\", \"chat.manage\", \"crm.manage\", \"payments.manage\", \"media.manage\", \"finance.manage\", \"reports.manage\"]' WHERE \"Name\" = 'Supervisor';");
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[\"users.manage\", \"watch_requests.manage\", \"community.manage\", \"comments.manage\", \"tasks.manage\", \"chat.manage\", \"crm.manage\", \"payments.manage\"]' WHERE \"Name\" = 'Staff';");
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[\"comments.manage\", \"community.manage\", \"exams.manage\", \"watch_requests.manage\", \"tasks.manage\", \"chat.manage\"]' WHERE \"Name\" = 'Assistant';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[]' WHERE \"Name\" IN ('Supervisor', 'Staff', 'Assistant');");
        }
    }
}
