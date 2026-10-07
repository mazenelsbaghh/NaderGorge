using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonMimStudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "higgsfield_mcp_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProtectedSession = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_higgsfield_mcp_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_higgsfield_mcp_connections_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_mim_studios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    DocumentJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_mim_studios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lesson_mim_studios_lesson_videos_SourceVideoId",
                        column: x => x.SourceVideoId,
                        principalTable: "lesson_videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_mim_studios_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lesson_mim_studios_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_higgsfield_mcp_connections_AdminUserId",
                table: "higgsfield_mcp_connections",
                column: "AdminUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_mim_studios_LessonId",
                table: "lesson_mim_studios",
                column: "LessonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_mim_studios_SourceVideoId",
                table: "lesson_mim_studios",
                column: "SourceVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_mim_studios_UpdatedByUserId",
                table: "lesson_mim_studios",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "higgsfield_mcp_connections");

            migrationBuilder.DropTable(
                name: "lesson_mim_studios");
        }
    }
}
