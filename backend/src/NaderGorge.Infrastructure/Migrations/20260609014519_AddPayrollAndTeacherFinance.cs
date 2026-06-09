using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollAndTeacherFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_code_activation_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionEarned = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_code_activation_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_access_code_activation_logs_access_codes_AccessCodeId",
                        column: x => x.AccessCodeId,
                        principalTable: "access_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_access_code_activation_logs_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_access_code_activation_logs_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_access_code_activation_logs_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payroll_records_employee_profiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payroll_records_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "teacher_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalEarnings = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_accounts_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HandledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    HandledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_payouts_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teacher_payouts_users_HandledByUserId",
                        column: x => x.HandledByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "payroll_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payroll_adjustments_payroll_records_PayrollRecordId",
                        column: x => x.PayrollRecordId,
                        principalTable: "payroll_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_code_activation_logs_AccessCodeId",
                table: "access_code_activation_logs",
                column: "AccessCodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_access_code_activation_logs_PackageId",
                table: "access_code_activation_logs",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_access_code_activation_logs_StudentId",
                table: "access_code_activation_logs",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_access_code_activation_logs_TeacherId",
                table: "access_code_activation_logs",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_adjustments_PayrollRecordId",
                table: "payroll_adjustments",
                column: "PayrollRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_records_ApprovedByUserId",
                table: "payroll_records",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_records_EmployeeProfileId_Month_Year",
                table: "payroll_records",
                columns: new[] { "EmployeeProfileId", "Month", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_accounts_TeacherId",
                table: "teacher_accounts",
                column: "TeacherId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payouts_HandledByUserId",
                table: "teacher_payouts",
                column: "HandledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payouts_Status",
                table: "teacher_payouts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_payouts_TeacherId",
                table: "teacher_payouts",
                column: "TeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_code_activation_logs");

            migrationBuilder.DropTable(
                name: "payroll_adjustments");

            migrationBuilder.DropTable(
                name: "teacher_accounts");

            migrationBuilder.DropTable(
                name: "teacher_payouts");

            migrationBuilder.DropTable(
                name: "payroll_records");
        }
    }
}
