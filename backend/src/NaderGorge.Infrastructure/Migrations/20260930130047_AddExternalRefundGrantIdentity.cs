using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalRefundGrantIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccessGrantId",
                table: "platform_refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_refunds_AccessGrantId",
                table: "platform_refunds",
                column: "AccessGrantId",
                unique: true,
                filter: "\"AccessGrantId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_platform_refunds_student_access_grants_AccessGrantId",
                table: "platform_refunds",
                column: "AccessGrantId",
                principalTable: "student_access_grants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_platform_refunds_student_access_grants_AccessGrantId",
                table: "platform_refunds");

            migrationBuilder.DropIndex(
                name: "IX_platform_refunds_AccessGrantId",
                table: "platform_refunds");

            migrationBuilder.DropColumn(
                name: "AccessGrantId",
                table: "platform_refunds");
        }
    }
}
