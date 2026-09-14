using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureSystemRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO roles
                    ("Id", "Name", "Type", "PermissionsJson", "AllowedDomain",
                     "AllowedNavbarItemsJson", "CreatedAt", "UpdatedAt")
                VALUES
                    ('16600000-0000-0000-0000-000000000101', 'Admin', 1, '[]', 'admin', '[]', TIMESTAMPTZ '2026-07-26 00:00:00+00', NULL),
                    ('16600000-0000-0000-0000-000000000102', 'Teacher', 2, '["content.manage","exams.manage","comments.manage"]', 'teacher', '[]', TIMESTAMPTZ '2026-07-26 00:00:00+00', NULL),
                    ('16600000-0000-0000-0000-000000000103', 'Assistant', 3, '["comments.manage","community.manage","exams.manage","watch_requests.manage","tasks.manage","chat.manage"]', 'assistant', '[]', TIMESTAMPTZ '2026-07-26 00:00:00+00', NULL),
                    ('16600000-0000-0000-0000-000000000104', 'Student', 4, '[]', 'student', '[]', TIMESTAMPTZ '2026-07-26 00:00:00+00', NULL)
                ON CONFLICT ("Name") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM roles role
                WHERE role."Id" IN (
                    '16600000-0000-0000-0000-000000000101',
                    '16600000-0000-0000-0000-000000000102',
                    '16600000-0000-0000-0000-000000000103',
                    '16600000-0000-0000-0000-000000000104'
                )
                AND NOT EXISTS (
                    SELECT 1 FROM user_roles user_role
                    WHERE user_role."RoleId" = role."Id"
                );
                """);
        }
    }
}
