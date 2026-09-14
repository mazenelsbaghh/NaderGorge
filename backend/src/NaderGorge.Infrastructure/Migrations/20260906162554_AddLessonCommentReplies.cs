using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonCommentReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "lesson_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_comments_LessonId_ParentCommentId_CreatedAt",
                table: "lesson_comments",
                columns: new[] { "LessonId", "ParentCommentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_comments_ParentCommentId",
                table: "lesson_comments",
                column: "ParentCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_comments_lesson_comments_ParentCommentId",
                table: "lesson_comments",
                column: "ParentCommentId",
                principalTable: "lesson_comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lesson_comments_lesson_comments_ParentCommentId",
                table: "lesson_comments");

            migrationBuilder.DropIndex(
                name: "IX_lesson_comments_LessonId_ParentCommentId_CreatedAt",
                table: "lesson_comments");

            migrationBuilder.DropIndex(
                name: "IX_lesson_comments_ParentCommentId",
                table: "lesson_comments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "lesson_comments");
        }
    }
}
