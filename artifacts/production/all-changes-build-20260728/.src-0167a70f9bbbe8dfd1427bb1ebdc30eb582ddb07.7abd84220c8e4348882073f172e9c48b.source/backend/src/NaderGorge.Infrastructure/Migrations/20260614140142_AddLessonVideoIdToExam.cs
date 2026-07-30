using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonVideoIdToExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lesson_videos_exams_ExamId",
                table: "lesson_videos");

            migrationBuilder.AddColumn<Guid>(
                name: "LessonVideoId",
                table: "exams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exams_LessonVideoId",
                table: "exams",
                column: "LessonVideoId");

            migrationBuilder.AddForeignKey(
                name: "FK_exams_lesson_videos_LessonVideoId",
                table: "exams",
                column: "LessonVideoId",
                principalTable: "lesson_videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_videos_exams_ExamId",
                table: "lesson_videos",
                column: "ExamId",
                principalTable: "exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exams_lesson_videos_LessonVideoId",
                table: "exams");

            migrationBuilder.DropForeignKey(
                name: "FK_lesson_videos_exams_ExamId",
                table: "lesson_videos");

            migrationBuilder.DropIndex(
                name: "IX_exams_LessonVideoId",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "LessonVideoId",
                table: "exams");

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_videos_exams_ExamId",
                table: "lesson_videos",
                column: "ExamId",
                principalTable: "exams",
                principalColumn: "Id");
        }
    }
}
