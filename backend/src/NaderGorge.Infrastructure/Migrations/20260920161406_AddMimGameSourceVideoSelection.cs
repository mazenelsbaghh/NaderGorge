using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMimGameSourceVideoSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DraftSourceVideoId",
                table: "lesson_mim_games",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GenerationSourceVideoId",
                table: "lesson_mim_games",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedSourceVideoId",
                table: "lesson_mim_games",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftSourceVideoId",
                table: "lesson_mim_games");

            migrationBuilder.DropColumn(
                name: "GenerationSourceVideoId",
                table: "lesson_mim_games");

            migrationBuilder.DropColumn(
                name: "PublishedSourceVideoId",
                table: "lesson_mim_games");
        }
    }
}
