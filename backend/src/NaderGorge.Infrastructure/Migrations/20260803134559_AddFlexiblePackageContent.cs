using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlexiblePackageContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemContainer",
                table: "terms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContentMode",
                table: "packages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "TermWithSections");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemContainer",
                table: "content_sections",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystemContainer",
                table: "terms");

            migrationBuilder.DropColumn(
                name: "ContentMode",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "IsSystemContainer",
                table: "content_sections");
        }
    }
}
