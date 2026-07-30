using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260613161000_FixFindTheMistakeMissingFields")]
    public partial class FixFindTheMistakeMissingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseText",
                table: "question_bank_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MistakeStartIndex",
                table: "question_bank_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MistakeEndIndex",
                table: "question_bank_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseText",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "MistakeStartIndex",
                table: "question_bank_items");

            migrationBuilder.DropColumn(
                name: "MistakeEndIndex",
                table: "question_bank_items");
        }
    }
}
