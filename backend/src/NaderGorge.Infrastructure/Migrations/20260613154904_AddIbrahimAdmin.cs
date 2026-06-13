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
            var userId = new System.Guid("d36c2e35-512c-497b-b8c7-43df9ac3b123");
            var roleId = new System.Guid("cf96578e-27c7-402e-b394-740e805c5f65"); // Admin Role ID
            var passwordHash = "$2b$12$HU77lFd.spR4jOIQaDPxkeZJe6wMc84doMdUsaKyO2NBtveg7jSo."; // Password: "01272629000"

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "CreatedAt", "FullName", "PhoneNumber", "PasswordHash", "IsActive", "IsProfileComplete", "SuspensionReason", "UpdatedAt" },
                values: new object[,]
                {
                    { userId, System.DateTime.UtcNow, "ابراهيم", "01272629000", passwordHash, true, true, null, null }
                });

            migrationBuilder.InsertData(
                table: "user_roles",
                columns: new[] { "UserId", "RoleId" },
                values: new object[,]
                {
                    { userId, roleId }
                });
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
