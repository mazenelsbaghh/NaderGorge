using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeNumber",
                table: "employee_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmploymentStatus",
                table: "employee_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "HireDate",
                table: "employee_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TerminationDate",
                table: "employee_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkMode",
                table: "employee_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE employee_profiles
                SET "EmployeeNumber" = 'EMP-' || UPPER(REPLACE("Id"::text, '-', '')),
                    "HireDate" = COALESCE("CreatedAt"::date, CURRENT_DATE)
                WHERE "EmployeeNumber" IS NULL OR "HireDate" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeNumber",
                table: "employee_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "HireDate",
                table: "employee_profiles",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_profiles_EmployeeNumber",
                table: "employee_profiles",
                column: "EmployeeNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_employee_profiles_EmployeeNumber",
                table: "employee_profiles");

            migrationBuilder.DropColumn(
                name: "EmployeeNumber",
                table: "employee_profiles");

            migrationBuilder.DropColumn(
                name: "EmploymentStatus",
                table: "employee_profiles");

            migrationBuilder.DropColumn(
                name: "HireDate",
                table: "employee_profiles");

            migrationBuilder.DropColumn(
                name: "TerminationDate",
                table: "employee_profiles");

            migrationBuilder.DropColumn(
                name: "WorkMode",
                table: "employee_profiles");
        }
    }
}
