using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StudentProfileV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FatherDateOfBirth",
                table: "student_profiles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MotherDateOfBirth",
                table: "student_profiles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherPhone",
                table: "student_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "student_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolName",
                table: "student_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolType",
                table: "student_profiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FatherDateOfBirth",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "MotherDateOfBirth",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "MotherPhone",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "SchoolName",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "SchoolType",
                table: "student_profiles");
        }
    }
}
