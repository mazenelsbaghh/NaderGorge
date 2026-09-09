using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreserveHomeworkAttemptScoreScale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PassingScoreSnapshot",
                table: "homework_submissions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalScoreSnapshot",
                table: "homework_submissions",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassingScoreSnapshot",
                table: "homework_submissions");

            migrationBuilder.DropColumn(
                name: "TotalScoreSnapshot",
                table: "homework_submissions");
        }
    }
}
