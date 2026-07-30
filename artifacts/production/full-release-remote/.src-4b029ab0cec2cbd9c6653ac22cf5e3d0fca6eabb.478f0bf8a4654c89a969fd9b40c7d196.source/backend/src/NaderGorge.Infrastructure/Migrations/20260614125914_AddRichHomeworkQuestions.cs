using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRichHomeworkQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "homework_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseText",
                table: "homework_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HintText",
                table: "homework_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MistakeEndIndex",
                table: "homework_questions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MistakeStartIndex",
                table: "homework_questions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WrittenCorrection",
                table: "homework_questions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "homework_questions");

            migrationBuilder.DropColumn(
                name: "BaseText",
                table: "homework_questions");

            migrationBuilder.DropColumn(
                name: "HintText",
                table: "homework_questions");

            migrationBuilder.DropColumn(
                name: "MistakeEndIndex",
                table: "homework_questions");

            migrationBuilder.DropColumn(
                name: "MistakeStartIndex",
                table: "homework_questions");

            migrationBuilder.DropColumn(
                name: "WrittenCorrection",
                table: "homework_questions");
        }
    }
}
