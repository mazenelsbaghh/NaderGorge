using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrRecruitmentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_employee_lifecycle_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employee_lifecycle_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_employee_lifecycle_tasks_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_offboarding_processes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastWorkingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    BlockersJson = table.Column<string>(type: "jsonb", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_offboarding_processes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_offboarding_processes_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_requisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequisitionNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Openings = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Requirements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_requisitions", x => x.Id);
                    table.CheckConstraint("CK_hr_requisition_openings", "\"Openings\" > 0");
                    table.ForeignKey(
                        name: "FK_hr_requisitions_hr_organization_units_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "hr_organization_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    CvAssetReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EmployeeProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_candidates_employee_profiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_candidates_hr_requisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "hr_requisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_candidate_interviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InterviewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    Feedback = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_candidate_interviews", x => x.Id);
                    table.CheckConstraint("CK_hr_interview_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 100)");
                    table.ForeignKey(
                        name: "FK_hr_candidate_interviews_hr_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "hr_candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_candidate_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BaseSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProposedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_candidate_offers", x => x.Id);
                    table.CheckConstraint("CK_hr_offer_salary", "\"BaseSalary\" >= 0");
                    table.ForeignKey(
                        name: "FK_hr_candidate_offers_hr_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "hr_candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_candidate_interviews_CandidateId",
                table: "hr_candidate_interviews",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_candidate_interviews_InterviewerUserId_ScheduledAt",
                table: "hr_candidate_interviews",
                columns: new[] { "InterviewerUserId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_candidate_offers_CandidateId",
                table: "hr_candidate_offers",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_candidate_offers_OfferNumber",
                table: "hr_candidate_offers",
                column: "OfferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_candidates_EmployeeProfileId",
                table: "hr_candidates",
                column: "EmployeeProfileId",
                unique: true,
                filter: "\"EmployeeProfileId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hr_candidates_RequisitionId_PhoneNumber",
                table: "hr_candidates",
                columns: new[] { "RequisitionId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_lifecycle_tasks_EmployeeId_Phase",
                table: "hr_employee_lifecycle_tasks",
                columns: new[] { "EmployeeId", "Phase" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_lifecycle_tasks_State_DueAt",
                table: "hr_employee_lifecycle_tasks",
                columns: new[] { "State", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_offboarding_processes_EmployeeId",
                table: "hr_offboarding_processes",
                column: "EmployeeId",
                unique: true,
                filter: "\"State\" <> 3 AND \"State\" <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_hr_requisitions_OrganizationUnitId",
                table: "hr_requisitions",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_requisitions_RequisitionNumber",
                table: "hr_requisitions",
                column: "RequisitionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_requisitions_State_CreatedAt",
                table: "hr_requisitions",
                columns: new[] { "State", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_candidate_interviews");

            migrationBuilder.DropTable(
                name: "hr_candidate_offers");

            migrationBuilder.DropTable(
                name: "hr_employee_lifecycle_tasks");

            migrationBuilder.DropTable(
                name: "hr_offboarding_processes");

            migrationBuilder.DropTable(
                name: "hr_candidates");

            migrationBuilder.DropTable(
                name: "hr_requisitions");
        }
    }
}
