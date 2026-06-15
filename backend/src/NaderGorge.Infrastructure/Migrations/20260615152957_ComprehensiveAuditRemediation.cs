using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComprehensiveAuditRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_homework_submissions_HomeworkId",
                table: "homework_submissions");

            migrationBuilder.AddColumn<string>(
                name: "OccurrenceKey",
                table: "warning_events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PasswordResetVersion",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_warning_events_OccurrenceKey",
                table: "warning_events",
                column: "OccurrenceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_homework_submissions_HomeworkId_StudentId",
                table: "homework_submissions",
                columns: new[] { "HomeworkId", "StudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_warning_events_OccurrenceKey",
                table: "warning_events");

            migrationBuilder.DropIndex(
                name: "IX_homework_submissions_HomeworkId_StudentId",
                table: "homework_submissions");

            migrationBuilder.DropColumn(
                name: "OccurrenceKey",
                table: "warning_events");

            migrationBuilder.DropColumn(
                name: "PasswordResetVersion",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_homework_submissions_HomeworkId",
                table: "homework_submissions",
                column: "HomeworkId");
        }
    }
}
