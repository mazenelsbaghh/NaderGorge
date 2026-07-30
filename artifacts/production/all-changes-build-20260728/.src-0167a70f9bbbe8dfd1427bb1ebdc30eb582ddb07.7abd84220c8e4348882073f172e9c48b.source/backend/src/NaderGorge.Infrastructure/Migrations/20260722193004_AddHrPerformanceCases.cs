using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPerformanceCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_employee_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    IsConfidential = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employee_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_employee_cases_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_performance_cycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_performance_cycles", x => x.Id);
                    table.CheckConstraint("CK_hr_performance_cycle_dates", "\"EndsOn\" >= \"StartsOn\"");
                });

            migrationBuilder.CreateTable(
                name: "hr_case_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_case_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_case_evidence_hr_employee_cases_EmployeeCaseId",
                        column: x => x.EmployeeCaseId,
                        principalTable: "hr_employee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_case_responses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Response = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    AttachmentReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_case_responses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_case_responses_hr_employee_cases_EmployeeCaseId",
                        column: x => x.EmployeeCaseId,
                        principalTable: "hr_employee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_disciplinary_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FinancialAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollLineItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_disciplinary_actions", x => x.Id);
                    table.CheckConstraint("CK_hr_disciplinary_financial", "\"Type\" <> 2 OR \"FinancialAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_hr_disciplinary_actions_hr_employee_cases_EmployeeCaseId",
                        column: x => x.EmployeeCaseId,
                        principalTable: "hr_employee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_disciplinary_actions_hr_payroll_line_items_PayrollLineIt~",
                        column: x => x.PayrollLineItemId,
                        principalTable: "hr_payroll_line_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_performance_goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformanceCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_performance_goals", x => x.Id);
                    table.CheckConstraint("CK_hr_performance_goal_weight", "\"Weight\" > 0 AND \"Weight\" <= 100");
                    table.ForeignKey(
                        name: "FK_hr_performance_goals_hr_performance_cycles_PerformanceCycle~",
                        column: x => x.PerformanceCycleId,
                        principalTable: "hr_performance_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_performance_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformanceCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoresJson = table.Column<string>(type: "jsonb", nullable: false),
                    WeightedScore = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AppealReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AppealResolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_performance_reviews", x => x.Id);
                    table.CheckConstraint("CK_hr_performance_review_score", "\"WeightedScore\" >= 0 AND \"WeightedScore\" <= 100");
                    table.ForeignKey(
                        name: "FK_hr_performance_reviews_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_performance_reviews_hr_performance_cycles_PerformanceCyc~",
                        column: x => x.PerformanceCycleId,
                        principalTable: "hr_performance_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_case_evidence_EmployeeCaseId_ContentHash",
                table: "hr_case_evidence",
                columns: new[] { "EmployeeCaseId", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_case_responses_EmployeeCaseId",
                table: "hr_case_responses",
                column: "EmployeeCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_disciplinary_actions_EmployeeCaseId",
                table: "hr_disciplinary_actions",
                column: "EmployeeCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_disciplinary_actions_PayrollLineItemId",
                table: "hr_disciplinary_actions",
                column: "PayrollLineItemId",
                unique: true,
                filter: "\"PayrollLineItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_cases_CaseNumber",
                table: "hr_employee_cases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_cases_EmployeeId_State_IsConfidential",
                table: "hr_employee_cases",
                columns: new[] { "EmployeeId", "State", "IsConfidential" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_performance_cycles_StartsOn_EndsOn",
                table: "hr_performance_cycles",
                columns: new[] { "StartsOn", "EndsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_performance_goals_PerformanceCycleId_Name",
                table: "hr_performance_goals",
                columns: new[] { "PerformanceCycleId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_performance_reviews_EmployeeId",
                table: "hr_performance_reviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_performance_reviews_PerformanceCycleId_EmployeeId",
                table: "hr_performance_reviews",
                columns: new[] { "PerformanceCycleId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_case_evidence");

            migrationBuilder.DropTable(
                name: "hr_case_responses");

            migrationBuilder.DropTable(
                name: "hr_disciplinary_actions");

            migrationBuilder.DropTable(
                name: "hr_performance_goals");

            migrationBuilder.DropTable(
                name: "hr_performance_reviews");

            migrationBuilder.DropTable(
                name: "hr_employee_cases");

            migrationBuilder.DropTable(
                name: "hr_performance_cycles");
        }
    }
}
