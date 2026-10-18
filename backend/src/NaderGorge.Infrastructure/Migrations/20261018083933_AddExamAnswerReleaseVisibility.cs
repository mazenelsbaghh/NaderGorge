using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAnswerReleaseVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                table: "notification_events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAnswersToStudents",
                table: "exams",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_events_ActionUrl",
                table: "notification_events",
                column: "ActionUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notification_events_ActionUrl",
                table: "notification_events");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                table: "notification_events");

            migrationBuilder.DropColumn(
                name: "ShowAnswersToStudents",
                table: "exams");
        }
    }
}
