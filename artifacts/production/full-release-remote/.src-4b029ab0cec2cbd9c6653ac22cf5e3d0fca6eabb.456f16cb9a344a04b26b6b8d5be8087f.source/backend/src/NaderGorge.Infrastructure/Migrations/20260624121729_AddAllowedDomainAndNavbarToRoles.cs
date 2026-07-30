using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowedDomainAndNavbarToRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedDomain",
                table: "roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "all");

            migrationBuilder.AddColumn<string>(
                name: "AllowedNavbarItemsJson",
                table: "roles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedDomain",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "AllowedNavbarItemsJson",
                table: "roles");
        }
    }
}
