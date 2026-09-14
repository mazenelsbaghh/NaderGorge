using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

public partial class GrantStaffStudentManagementAndReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE roles
            SET \"PermissionsJson\" = '[\"users.manage\",\"watch_requests.manage\",\"community.manage\",\"comments.manage\",\"tasks.manage\",\"chat.manage\",\"crm.manage\",\"payments.manage\",\"reports.manage\"]',
                \"AllowedDomain\" = 'assistant'
            WHERE \"Name\" = 'Staff';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE roles
            SET \"PermissionsJson\" = '[\"users.manage\",\"watch_requests.manage\",\"community.manage\",\"comments.manage\",\"tasks.manage\",\"chat.manage\",\"crm.manage\",\"payments.manage\"]',
                \"AllowedDomain\" = 'assistant'
            WHERE \"Name\" = 'Staff';
            """);
    }
}
