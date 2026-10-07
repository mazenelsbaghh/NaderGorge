using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMimSceneGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SourceVideoId",
                table: "lesson_mim_studios",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "GenerationStartedAt",
                table: "lesson_mim_studios",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mim_scene_videos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SceneIndex = table.Column<int>(type: "integer", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScriptVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    QuoteText = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    QuoteExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mim_scene_videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mim_scene_videos_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mim_scene_videos_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mim_scene_videos_AdminUserId",
                table: "mim_scene_videos",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_mim_scene_videos_LessonId_SceneIndex",
                table: "mim_scene_videos",
                columns: new[] { "LessonId", "SceneIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mim_scene_videos");

            migrationBuilder.DropColumn(
                name: "GenerationStartedAt",
                table: "lesson_mim_studios");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceVideoId",
                table: "lesson_mim_studios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
