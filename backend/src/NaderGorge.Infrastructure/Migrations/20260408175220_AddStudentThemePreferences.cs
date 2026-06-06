using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentThemePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DarkThemePaletteId",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LightThemePaletteId",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentMode",
                table: "student_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "light");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DarkThemePaletteId",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "LightThemePaletteId",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "CurrentMode",
                table: "student_profiles");
        }
    }
}
