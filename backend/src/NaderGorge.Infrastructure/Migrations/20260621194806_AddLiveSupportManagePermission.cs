using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSupportManagePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[\"users.manage\", \"content.manage\", \"exams.manage\", \"codes.manage\", \"watch_requests.manage\", \"community.manage\", \"comments.manage\", \"hr.manage\", \"tasks.manage\", \"chat.manage\", \"crm.manage\", \"payments.manage\", \"media.manage\", \"finance.manage\", \"reports.manage\", \"live_support.manage\"]' WHERE \"Name\" = 'Supervisor';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE roles SET \"PermissionsJson\" = '[\"users.manage\", \"content.manage\", \"exams.manage\", \"codes.manage\", \"watch_requests.manage\", \"community.manage\", \"comments.manage\", \"hr.manage\", \"tasks.manage\", \"chat.manage\", \"crm.manage\", \"payments.manage\", \"media.manage\", \"finance.manage\", \"reports.manage\"]' WHERE \"Name\" = 'Supervisor';");
        }
    }
}
