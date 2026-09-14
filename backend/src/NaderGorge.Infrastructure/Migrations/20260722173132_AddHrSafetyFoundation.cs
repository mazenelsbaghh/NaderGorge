using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrSafetyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_logs_employee_profiles_EmployeeId",
                table: "attendance_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_profiles_users_UserId",
                table: "employee_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_vacations_employee_profiles_EmployeeId",
                table: "employee_vacations");

            migrationBuilder.DropForeignKey(
                name: "FK_payroll_adjustments_payroll_records_PayrollRecordId",
                table: "payroll_adjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_payroll_records_employee_profiles_EmployeeProfileId",
                table: "payroll_records");

            migrationBuilder.CreateTable(
                name: "hr_idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResultEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_module_rollouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ReadTarget = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WriteTarget = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReconciliationBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_module_rollouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_idempotency_records_ExpiresAt",
                table: "hr_idempotency_records",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_hr_idempotency_records_Scope_ActorUserId_Key",
                table: "hr_idempotency_records",
                columns: new[] { "Scope", "ActorUserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_module_rollouts_Module",
                table: "hr_module_rollouts",
                column: "Module",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_logs_employee_profiles_EmployeeId",
                table: "attendance_logs",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_profiles_users_UserId",
                table: "employee_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_vacations_employee_profiles_EmployeeId",
                table: "employee_vacations",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_adjustments_payroll_records_PayrollRecordId",
                table: "payroll_adjustments",
                column: "PayrollRecordId",
                principalTable: "payroll_records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_records_employee_profiles_EmployeeProfileId",
                table: "payroll_records",
                column: "EmployeeProfileId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_logs_employee_profiles_EmployeeId",
                table: "attendance_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_profiles_users_UserId",
                table: "employee_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_vacations_employee_profiles_EmployeeId",
                table: "employee_vacations");

            migrationBuilder.DropForeignKey(
                name: "FK_payroll_adjustments_payroll_records_PayrollRecordId",
                table: "payroll_adjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_payroll_records_employee_profiles_EmployeeProfileId",
                table: "payroll_records");

            migrationBuilder.DropTable(
                name: "hr_idempotency_records");

            migrationBuilder.DropTable(
                name: "hr_module_rollouts");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_logs_employee_profiles_EmployeeId",
                table: "attendance_logs",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_profiles_users_UserId",
                table: "employee_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_vacations_employee_profiles_EmployeeId",
                table: "employee_vacations",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_adjustments_payroll_records_PayrollRecordId",
                table: "payroll_adjustments",
                column: "PayrollRecordId",
                principalTable: "payroll_records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payroll_records_employee_profiles_EmployeeProfileId",
                table: "payroll_records",
                column: "EmployeeProfileId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
