using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentAttemptDefinitionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefinitionSnapshotJson",
                table: "student_exam_attempts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionSnapshotJson",
                table: "homework_submissions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefinitionSnapshotJson",
                table: "student_exam_attempts");

            migrationBuilder.DropColumn(
                name: "DefinitionSnapshotJson",
                table: "homework_submissions");
        }
    }
}
