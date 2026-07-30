using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupervisorAndStaffRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var seededAt = new System.DateTime(
                2026,
                6,
                7,
                0,
                0,
                0,
                System.DateTimeKind.Unspecified);

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "Id", "CreatedAt", "Name", "PermissionsJson", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { new System.Guid("c77894a7-8910-4b3d-8e7c-b26a5cd5f1de"), seededAt, "Supervisor", "[]", 7, null },
                    { new System.Guid("8e2b8c94-1a3b-4836-8c7c-9b7e3da342c8"), seededAt, "Staff", "[]", 8, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Name",
                keyValues: new object[] { "Supervisor", "Staff" });
        }
    }
}
