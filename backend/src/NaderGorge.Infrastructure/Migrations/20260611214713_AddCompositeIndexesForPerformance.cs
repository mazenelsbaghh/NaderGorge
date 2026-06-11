using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexesForPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_PerformedByUserId",
                table: "audit_logs");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_comments_LessonId_CreatedAt",
                table: "lesson_comments",
                columns: new[] { "LessonId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_comments_Status_CreatedAt",
                table: "lesson_comments",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_PerformedByUserId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "PerformedByUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_comments_LessonId_CreatedAt",
                table: "lesson_comments");

            migrationBuilder.DropIndex(
                name: "IX_lesson_comments_Status_CreatedAt",
                table: "lesson_comments");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_PerformedByUserId_CreatedAt",
                table: "audit_logs");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_PerformedByUserId",
                table: "audit_logs",
                column: "PerformedByUserId");
        }
    }
}
