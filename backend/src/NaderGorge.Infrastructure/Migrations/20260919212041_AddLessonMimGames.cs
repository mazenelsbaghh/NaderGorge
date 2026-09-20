using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonMimGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lesson_mim_games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftContentJson = table.Column<string>(type: "jsonb", nullable: true),
                    DraftFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PublishedContentJson = table.Column<string>(type: "jsonb", nullable: true),
                    PublishedFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CurrentGenerationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GenerationStartedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GenerationExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_mim_games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lesson_mim_games_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_mim_games_LessonId",
                table: "lesson_mim_games",
                column: "LessonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_mim_games");
        }
    }
}
