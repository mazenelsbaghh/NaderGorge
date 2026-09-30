using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherFinancialReviewOperationIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewActorUserId",
                table: "teacher_financial_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "teacher_financial_allocations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewOperationId",
                table: "teacher_financial_allocations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_allocations_ReviewOperationId",
                table: "teacher_financial_allocations",
                column: "ReviewOperationId",
                unique: true,
                filter: "\"ReviewOperationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_teacher_financial_allocations_ReviewOperationId",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "ReviewActorUserId",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "ReviewOperationId",
                table: "teacher_financial_allocations");
        }
    }
}
