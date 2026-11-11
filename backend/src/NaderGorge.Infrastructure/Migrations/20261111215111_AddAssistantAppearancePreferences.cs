using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantAppearancePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assistant_appearance_preferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LightPaletteId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DarkPaletteId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrentMode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_appearance_preferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_assistant_appearance_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_appearance_preferences");
        }
    }
}
