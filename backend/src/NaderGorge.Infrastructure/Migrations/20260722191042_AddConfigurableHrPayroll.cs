using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurableHrPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_employee_compensations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employee_compensations", x => x.Id);
                    table.CheckConstraint("CK_hr_compensation_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_hr_employee_compensations_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_pay_components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                    IsInsurable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_pay_components", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_payroll_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CutoffAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalGross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalNet = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinanceReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinanceReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GmApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GmApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SourceDataVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReconciliationHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_payroll_runs", x => x.Id);
                    table.CheckConstraint("CK_hr_payroll_run_period", "\"PeriodEnd\" >= \"PeriodStart\"");
                });

            migrationBuilder.CreateTable(
                name: "hr_payroll_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Expression = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_payroll_rules", x => x.Id);
                    table.CheckConstraint("CK_hr_payroll_rule_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.CheckConstraint("CK_hr_payroll_rule_version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_hr_payroll_rules_hr_pay_components_PayComponentId",
                        column: x => x.PayComponentId,
                        principalTable: "hr_pay_components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_employee_payrolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeNumberSnapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EmployeeNameSnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BaseSalarySnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Gross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Deductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Net = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employee_payrolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_employee_payrolls_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employee_payrolls_hr_payroll_runs_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "hr_payroll_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_payroll_line_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeePayrollId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InputsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAdjustment = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_payroll_line_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_payroll_line_items_hr_employee_payrolls_EmployeePayrollId",
                        column: x => x.EmployeePayrollId,
                        principalTable: "hr_employee_payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_payroll_line_items_hr_pay_components_PayComponentId",
                        column: x => x.PayComponentId,
                        principalTable: "hr_pay_components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_payroll_line_items_hr_payroll_rules_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalTable: "hr_payroll_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_payslips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeePayrollId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AssetReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_payslips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_payslips_hr_employee_payrolls_EmployeePayrollId",
                        column: x => x.EmployeePayrollId,
                        principalTable: "hr_employee_payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_payroll_settlement_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalPayrollLineItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementPayrollRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_payroll_settlement_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_payroll_settlement_adjustments_hr_payroll_line_items_Ori~",
                        column: x => x.OriginalPayrollLineItemId,
                        principalTable: "hr_payroll_line_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_payroll_settlement_adjustments_hr_payroll_runs_Settlemen~",
                        column: x => x.SettlementPayrollRunId,
                        principalTable: "hr_payroll_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_compensations_EmployeeId_EffectiveFrom",
                table: "hr_employee_compensations",
                columns: new[] { "EmployeeId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_payrolls_EmployeeId",
                table: "hr_employee_payrolls",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_payrolls_PayrollRunId_EmployeeId",
                table: "hr_employee_payrolls",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_pay_components_Code",
                table: "hr_pay_components",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_line_items_EmployeePayrollId_SourceType_SourceId~",
                table: "hr_payroll_line_items",
                columns: new[] { "EmployeePayrollId", "SourceType", "SourceId", "PayComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_line_items_PayComponentId",
                table: "hr_payroll_line_items",
                column: "PayComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_line_items_RuleVersionId",
                table: "hr_payroll_line_items",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_rules_PayComponentId_EffectiveFrom_Version",
                table: "hr_payroll_rules",
                columns: new[] { "PayComponentId", "EffectiveFrom", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_runs_PeriodStart_PeriodEnd",
                table: "hr_payroll_runs",
                columns: new[] { "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_runs_RunNumber",
                table: "hr_payroll_runs",
                column: "RunNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_runs_Status",
                table: "hr_payroll_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_settlement_adjustments_OriginalPayrollLineItemId~",
                table: "hr_payroll_settlement_adjustments",
                columns: new[] { "OriginalPayrollLineItemId", "SettlementPayrollRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_settlement_adjustments_SettlementPayrollRunId",
                table: "hr_payroll_settlement_adjustments",
                column: "SettlementPayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_payslips_EmployeePayrollId_Version",
                table: "hr_payslips",
                columns: new[] { "EmployeePayrollId", "Version" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_final_payroll_line_mutation() RETURNS trigger AS $$
                DECLARE run_status integer;
                BEGIN
                    SELECT r."Status" INTO run_status
                    FROM hr_employee_payrolls ep JOIN hr_payroll_runs r ON r."Id" = ep."PayrollRunId"
                    WHERE ep."Id" = COALESCE(OLD."EmployeePayrollId", NEW."EmployeePayrollId");
                    IF run_status >= 4 THEN
                        RAISE EXCEPTION 'Final payroll lines are immutable';
                    END IF;
                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER trg_hr_payroll_line_immutable
                BEFORE UPDATE OR DELETE ON hr_payroll_line_items
                FOR EACH ROW EXECUTE FUNCTION prevent_final_payroll_line_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_hr_payroll_line_immutable ON hr_payroll_line_items; DROP FUNCTION IF EXISTS prevent_final_payroll_line_mutation();");
            migrationBuilder.DropTable(
                name: "hr_employee_compensations");

            migrationBuilder.DropTable(
                name: "hr_payroll_settlement_adjustments");

            migrationBuilder.DropTable(
                name: "hr_payslips");

            migrationBuilder.DropTable(
                name: "hr_payroll_line_items");

            migrationBuilder.DropTable(
                name: "hr_employee_payrolls");

            migrationBuilder.DropTable(
                name: "hr_payroll_rules");

            migrationBuilder.DropTable(
                name: "hr_payroll_runs");

            migrationBuilder.DropTable(
                name: "hr_pay_components");
        }
    }
}
