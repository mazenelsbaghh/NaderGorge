using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrFinancialRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_financial_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequestedInstallments = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AttachmentReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ApprovalInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_financial_requests", x => x.Id);
                    table.CheckConstraint("CK_hr_financial_request_amount", "\"Amount\" > 0 AND \"OutstandingBalance\" >= 0");
                    table.CheckConstraint("CK_hr_financial_request_installments", "\"RequestedInstallments\" BETWEEN 1 AND 60");
                    table.ForeignKey(
                        name: "FK_hr_financial_requests_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_payroll_input_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeePayrollId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollLineItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_payroll_input_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_payroll_input_sources_hr_employee_payrolls_EmployeePayro~",
                        column: x => x.EmployeePayrollId,
                        principalTable: "hr_employee_payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_payroll_input_sources_hr_payroll_line_items_PayrollLineI~",
                        column: x => x.PayrollLineItemId,
                        principalTable: "hr_payroll_line_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_financial_installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PayrollLineItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_financial_installments", x => x.Id);
                    table.CheckConstraint("CK_hr_financial_installment_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_hr_financial_installments_hr_financial_requests_FinancialRe~",
                        column: x => x.FinancialRequestId,
                        principalTable: "hr_financial_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_financial_installments_hr_payroll_line_items_PayrollLine~",
                        column: x => x.PayrollLineItemId,
                        principalTable: "hr_payroll_line_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_financial_installments_FinancialRequestId_Sequence",
                table: "hr_financial_installments",
                columns: new[] { "FinancialRequestId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_financial_installments_PayrollLineItemId",
                table: "hr_financial_installments",
                column: "PayrollLineItemId",
                unique: true,
                filter: "\"PayrollLineItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hr_financial_installments_State_DueDate",
                table: "hr_financial_installments",
                columns: new[] { "State", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_financial_requests_EmployeeId_State_CreatedAt",
                table: "hr_financial_requests",
                columns: new[] { "EmployeeId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_input_sources_EmployeePayrollId",
                table: "hr_payroll_input_sources",
                column: "EmployeePayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_input_sources_PayrollLineItemId",
                table: "hr_payroll_input_sources",
                column: "PayrollLineItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_input_sources_SourceType_SourceId",
                table: "hr_payroll_input_sources",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_financial_installments");

            migrationBuilder.DropTable(
                name: "hr_payroll_input_sources");

            migrationBuilder.DropTable(
                name: "hr_financial_requests");
        }
    }
}
