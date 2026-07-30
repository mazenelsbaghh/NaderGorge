using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrOrganizationContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_cost_centers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_cost_centers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_employment_contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProbationEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BaseSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TermsVersion = table.Column<int>(type: "integer", nullable: false),
                    TermsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employment_contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_employment_contracts_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_job_grades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_job_grades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_job_positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_job_positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_organization_units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_organization_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_organization_units_employee_profiles_ManagerEmployeeId",
                        column: x => x.ManagerEmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_organization_units_hr_organization_units_ParentId",
                        column: x => x.ParentId,
                        principalTable: "hr_organization_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_work_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    GeofenceRadiusMeters = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_work_locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_employment_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobGradeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ChangeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employment_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_employment_assignments_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employment_assignments_employee_profiles_ManagerEmployee~",
                        column: x => x.ManagerEmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employment_assignments_hr_cost_centers_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "hr_cost_centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employment_assignments_hr_job_grades_JobGradeId",
                        column: x => x.JobGradeId,
                        principalTable: "hr_job_grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employment_assignments_hr_job_positions_JobPositionId",
                        column: x => x.JobPositionId,
                        principalTable: "hr_job_positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employment_assignments_hr_organization_units_Organizatio~",
                        column: x => x.OrganizationUnitId,
                        principalTable: "hr_organization_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_employment_assignments_hr_work_locations_WorkLocationId",
                        column: x => x.WorkLocationId,
                        principalTable: "hr_work_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_cost_centers_Code",
                table: "hr_cost_centers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_CostCenterId",
                table: "hr_employment_assignments",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_EmployeeId_EffectiveFrom",
                table: "hr_employment_assignments",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_JobGradeId",
                table: "hr_employment_assignments",
                column: "JobGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_JobPositionId",
                table: "hr_employment_assignments",
                column: "JobPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_ManagerEmployeeId",
                table: "hr_employment_assignments",
                column: "ManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_OrganizationUnitId",
                table: "hr_employment_assignments",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_assignments_WorkLocationId",
                table: "hr_employment_assignments",
                column: "WorkLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_contracts_ContractNumber",
                table: "hr_employment_contracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_contracts_EmployeeId_StartDate",
                table: "hr_employment_contracts",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_job_grades_Code",
                table: "hr_job_grades",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_job_positions_Code",
                table: "hr_job_positions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_organization_units_Code",
                table: "hr_organization_units",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_organization_units_ManagerEmployeeId",
                table: "hr_organization_units",
                column: "ManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_organization_units_ParentId",
                table: "hr_organization_units",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_work_locations_Code",
                table: "hr_work_locations",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_employment_assignments");

            migrationBuilder.DropTable(
                name: "hr_employment_contracts");

            migrationBuilder.DropTable(
                name: "hr_cost_centers");

            migrationBuilder.DropTable(
                name: "hr_job_grades");

            migrationBuilder.DropTable(
                name: "hr_job_positions");

            migrationBuilder.DropTable(
                name: "hr_organization_units");

            migrationBuilder.DropTable(
                name: "hr_work_locations");
        }
    }
}
