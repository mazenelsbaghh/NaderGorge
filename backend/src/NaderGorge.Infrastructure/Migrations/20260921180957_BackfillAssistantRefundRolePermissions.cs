using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillAssistantRefundRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Roles"
                SET "PermissionsJson" = (
                    COALESCE(NULLIF("PermissionsJson", ''), '[]')::jsonb
                    || CASE
                        WHEN COALESCE(NULLIF("PermissionsJson", ''), '[]')::jsonb ? 'finance.refunds.view'
                        THEN '[]'::jsonb
                        ELSE '["finance.refunds.view"]'::jsonb
                    END
                    || CASE
                        WHEN COALESCE(NULLIF("PermissionsJson", ''), '[]')::jsonb ? 'finance.refunds.create'
                        THEN '[]'::jsonb
                        ELSE '["finance.refunds.create"]'::jsonb
                    END
                )::text
                WHERE "AllowedDomain" = 'assistant'
                  AND COALESCE(NULLIF("AllowedNavbarItemsJson", ''), '[]')::jsonb ? '/assistant/refunds';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Role permissions may be changed after this migration, so removing them during rollback would revoke legitimate access.
        }
    }
}
