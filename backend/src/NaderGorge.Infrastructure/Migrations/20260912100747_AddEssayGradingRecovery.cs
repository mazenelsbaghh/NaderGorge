using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEssayGradingRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AiNextRetryAt",
                table: "essay_submissions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_essay_submissions_Status_AiNextRetryAt_CreatedAt",
                table: "essay_submissions",
                columns: new[] { "Status", "AiNextRetryAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_essay_submissions_Status_AiNextRetryAt_CreatedAt",
                table: "essay_submissions");

            migrationBuilder.DropColumn(
                name: "AiNextRetryAt",
                table: "essay_submissions");
        }
    }
}
