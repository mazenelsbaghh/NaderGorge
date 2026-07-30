using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InlineExamsAndQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "question_bank_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ExamId",
                table: "lesson_videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_videos_ExamId",
                table: "lesson_videos",
                column: "ExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_videos_exams_ExamId",
                table: "lesson_videos",
                column: "ExamId",
                principalTable: "exams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lesson_videos_exams_ExamId",
                table: "lesson_videos");

            migrationBuilder.DropIndex(
                name: "IX_lesson_videos_ExamId",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "ExamId",
                table: "lesson_videos");
        }
    }
}
