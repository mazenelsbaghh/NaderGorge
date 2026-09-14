using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsurePlatformSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "PlatformSettings" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "Key" text NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Value" text NOT NULL,
                    CONSTRAINT "PK_PlatformSettings" PRIMARY KEY ("Id")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformSettings");
        }
    }
}
