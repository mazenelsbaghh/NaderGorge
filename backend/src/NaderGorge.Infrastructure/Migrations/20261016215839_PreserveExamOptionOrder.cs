using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreserveExamOptionOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "question_options",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH ordered_options AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "QuestionBankItemId" ORDER BY "CreatedAt", "Id"
                    )::integer AS position
                    FROM question_options
                )
                UPDATE question_options AS option
                SET "Order" = ordered_options.position
                FROM ordered_options
                WHERE option."Id" = ordered_options."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "question_options");
        }
    }
}
