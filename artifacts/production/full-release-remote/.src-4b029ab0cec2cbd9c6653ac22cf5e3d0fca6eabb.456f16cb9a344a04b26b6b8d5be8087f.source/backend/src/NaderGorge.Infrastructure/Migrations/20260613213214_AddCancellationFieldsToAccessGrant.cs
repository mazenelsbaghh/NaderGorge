using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationFieldsToAccessGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "student_access_grants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "student_access_grants",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByUserId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_CancelledByUserId",
                table: "student_access_grants",
                column: "CancelledByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_student_access_grants_users_CancelledByUserId",
                table: "student_access_grants",
                column: "CancelledByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_student_access_grants_users_CancelledByUserId",
                table: "student_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_CancelledByUserId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "student_access_grants");
        }
    }
}
