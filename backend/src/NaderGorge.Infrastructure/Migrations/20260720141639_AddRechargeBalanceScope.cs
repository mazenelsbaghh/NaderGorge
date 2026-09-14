using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRechargeBalanceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "recharge_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_TeacherId",
                table: "recharge_requests",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_recharge_requests_teacher_profiles_TeacherId",
                table: "recharge_requests",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_recharge_requests_teacher_profiles_TeacherId", table: "recharge_requests");
            migrationBuilder.DropIndex(name: "IX_recharge_requests_TeacherId", table: "recharge_requests");
            migrationBuilder.DropColumn(name: "TeacherId", table: "recharge_requests");
        }
    }
}
