using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixQuestionBankItemMissingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "question_bank_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HintText",
                table: "question_bank_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WrittenCorrection",
                table: "question_bank_items",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "HintText",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "WrittenCorrection",
                table: "question_bank_items");
        }
    }
}
