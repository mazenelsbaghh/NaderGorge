using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIbrahimAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var userId = "d36c2e35-512c-497b-b8c7-43df9ac3b123";
            var roleId = "cf96578e-27c7-402e-b394-740e805c5f65"; // Admin Role ID
            var passwordHash = "$2b$12$HU77lFd.spR4jOIQaDPxkeZJe6wMc84doMdUsaKyO2NBtveg7jSo."; // Password: "01272629000"

            // Ensure admin role exists (safe upsert — catches Id OR Name conflicts)
            migrationBuilder.Sql($@"
                INSERT INTO roles (""Id"", ""Name"", ""Type"", ""PermissionsJson"", ""CreatedAt"")
                SELECT '{roleId}', 'Admin', 0, '[]', NOW()
                WHERE NOT EXISTS (SELECT 1 FROM roles WHERE ""Name"" = 'Admin');
            ");

            // Insert user if not exists
            migrationBuilder.Sql($@"
                INSERT INTO users (""Id"", ""CreatedAt"", ""FullName"", ""PhoneNumber"", ""PasswordHash"", ""IsActive"", ""IsProfileComplete"")
                VALUES ('{userId}', NOW(), 'ابراهيم', '01272629000', '{passwordHash}', true, true)
                ON CONFLICT (""Id"") DO NOTHING;
            ");

            // Insert user role if not exists (use actual Admin role ID from DB)
            migrationBuilder.Sql($@"
                INSERT INTO user_roles (""UserId"", ""RoleId"")
                SELECT '{userId}', r.""Id""
                FROM roles r
                WHERE r.""Name"" = 'Admin'
                AND NOT EXISTS (
                    SELECT 1 FROM user_roles ur WHERE ur.""UserId"" = '{userId}' AND ur.""RoleId"" = r.""Id""
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM user_roles WHERE \"UserId\" = 'd36c2e35-512c-497b-b8c7-43df9ac3b123' AND \"RoleId\" = 'cf96578e-27c7-402e-b394-740e805c5f65';");

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValues: new object[] { new System.Guid("d36c2e35-512c-497b-b8c7-43df9ac3b123") });
        }
    }
}
