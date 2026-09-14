using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hr_payroll_runs_Status",
                table: "hr_payroll_runs");

            migrationBuilder.DropIndex(
                name: "IX_hr_employment_assignments_OrganizationUnitId",
                table: "hr_employment_assignments");

            migrationBuilder.DropIndex(
                name: "IX_hr_employee_payrolls_EmployeeId",
                table: "hr_employee_payrolls");

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_runs_Status_PeriodEnd",
                table: "hr_payroll_runs",
                columns: new[] { "Status", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_requests_State_StartDate_EndDate_EmployeeId",
                table: "hr_leave_requests",
                columns: new[] { "State", "StartDate", "EndDate", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_OrganizationUnitId_EffectiveFrom_~",
                table: "hr_employment_assignments",
                columns: new[] { "OrganizationUnitId", "EffectiveFrom", "EffectiveTo", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_payrolls_EmployeeId_Status_PayrollRunId",
                table: "hr_employee_payrolls",
                columns: new[] { "EmployeeId", "Status", "PayrollRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_profiles_EmploymentStatus_HireDate_TerminationDate",
                table: "employee_profiles",
                columns: new[] { "EmploymentStatus", "HireDate", "TerminationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hr_payroll_runs_Status_PeriodEnd",
                table: "hr_payroll_runs");

            migrationBuilder.DropIndex(
                name: "IX_hr_leave_requests_State_StartDate_EndDate_EmployeeId",
                table: "hr_leave_requests");

            migrationBuilder.DropIndex(
                name: "IX_hr_employment_assignments_OrganizationUnitId_EffectiveFrom_~",
                table: "hr_employment_assignments");

            migrationBuilder.DropIndex(
                name: "IX_hr_employee_payrolls_EmployeeId_Status_PayrollRunId",
                table: "hr_employee_payrolls");

            migrationBuilder.DropIndex(
                name: "IX_employee_profiles_EmploymentStatus_HireDate_TerminationDate",
                table: "employee_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_hr_payroll_runs_Status",
                table: "hr_payroll_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_OrganizationUnitId",
                table: "hr_employment_assignments",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_payrolls_EmployeeId",
                table: "hr_employee_payrolls",
                column: "EmployeeId");
        }
    }
}
