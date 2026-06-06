using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformedByToBalanceTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PerformedByUserId",
                table: "balance_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_balance_transactions_PerformedByUserId",
                table: "balance_transactions",
                column: "PerformedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_balance_transactions_users_PerformedByUserId",
                table: "balance_transactions",
                column: "PerformedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_balance_transactions_users_PerformedByUserId",
                table: "balance_transactions");

            migrationBuilder.DropIndex(
                name: "IX_balance_transactions_PerformedByUserId",
                table: "balance_transactions");

            migrationBuilder.DropColumn(
                name: "PerformedByUserId",
                table: "balance_transactions");
        }
    }
}
