using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoLearningConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLearningConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoLearningConfigurations_lesson_videos_LessonVideoId",
                        column: x => x.LessonVideoId,
                        principalTable: "lesson_videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoLearningEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Seconds = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Correct = table.Column<bool>(type: "boolean", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLearningEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoLearningEntries_lesson_videos_LessonVideoId",
                        column: x => x.LessonVideoId,
                        principalTable: "lesson_videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoLearningEntries_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoLearningConfigurations_LessonVideoId",
                table: "VideoLearningConfigurations",
                column: "LessonVideoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoLearningEntries_LessonVideoId_ConfigurationVersion_Kind",
                table: "VideoLearningEntries",
                columns: new[] { "LessonVideoId", "ConfigurationVersion", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoLearningEntries_StudentId_LessonVideoId_ConfigurationV~",
                table: "VideoLearningEntries",
                columns: new[] { "StudentId", "LessonVideoId", "ConfigurationVersion", "ActivityId", "Kind" },
                unique: true,
                filter: "\"Kind\" = 'answer'");

            migrationBuilder.CreateIndex(
                name: "IX_VideoLearningEntries_StudentId_LessonVideoId_SourceRevision",
                table: "VideoLearningEntries",
                columns: new[] { "StudentId", "LessonVideoId", "SourceRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoLearningConfigurations");

            migrationBuilder.DropTable(
                name: "VideoLearningEntries");
        }
    }
}
