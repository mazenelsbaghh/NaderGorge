using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentWelcomeState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstWelcomeCompletedAt",
                table: "student_profiles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastWelcomeDate",
                table: "student_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WelcomeClaimDate",
                table: "student_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WelcomeClaimExpiresAt",
                table: "student_profiles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WelcomeClaimToken",
                table: "student_profiles",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstWelcomeCompletedAt",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "LastWelcomeDate",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "WelcomeClaimDate",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "WelcomeClaimExpiresAt",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "WelcomeClaimToken",
                table: "student_profiles");
        }
    }
}
