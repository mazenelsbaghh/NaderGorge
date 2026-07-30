using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_work_calendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkingDaysMask = table.Column<int>(type: "integer", nullable: false),
                    HolidaysJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_work_calendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_shift_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    WorkCalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    GraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumBreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    OvertimeAfterMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_shift_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_shift_templates_hr_work_calendars_WorkCalendarId",
                        column: x => x.WorkCalendarId,
                        principalTable: "hr_work_calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_shift_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReplacesAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_shift_assignments", x => x.Id);
                    table.CheckConstraint("CK_hr_shift_assignments_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_hr_shift_assignments_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_shift_assignments_hr_shift_assignments_ReplacesAssignmen~",
                        column: x => x.ReplacesAssignmentId,
                        principalTable: "hr_shift_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_shift_assignments_hr_shift_templates_ShiftTemplateId",
                        column: x => x.ShiftTemplateId,
                        principalTable: "hr_shift_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_shift_assignments_users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_shift_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    StartsAt = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndsAt = table.Column<TimeSpan>(type: "interval", nullable: false),
                    UnpaidBreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    WorkDateRule = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_shift_segments", x => x.Id);
                    table.CheckConstraint("CK_hr_shift_segments_nonzero", "\"StartsAt\" <> \"EndsAt\"");
                    table.ForeignKey(
                        name: "FK_hr_shift_segments_hr_shift_templates_ShiftTemplateId",
                        column: x => x.ShiftTemplateId,
                        principalTable: "hr_shift_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_shift_swap_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ManagerDecisionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    HrDecisionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_shift_swap_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_shift_swap_requests_employee_profiles_RequesterEmployeeId",
                        column: x => x.RequesterEmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_shift_swap_requests_employee_profiles_TargetEmployeeId",
                        column: x => x.TargetEmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_shift_swap_requests_hr_shift_assignments_RequesterAssign~",
                        column: x => x.RequesterAssignmentId,
                        principalTable: "hr_shift_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_shift_swap_requests_hr_shift_assignments_TargetAssignmen~",
                        column: x => x.TargetAssignmentId,
                        principalTable: "hr_shift_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_assignments_EmployeeId_EffectiveFrom_EffectiveTo",
                table: "hr_shift_assignments",
                columns: new[] { "EmployeeId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_assignments_PublishedByUserId",
                table: "hr_shift_assignments",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_assignments_ReplacesAssignmentId",
                table: "hr_shift_assignments",
                column: "ReplacesAssignmentId",
                unique: true,
                filter: "\"ReplacesAssignmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_assignments_ShiftTemplateId",
                table: "hr_shift_assignments",
                column: "ShiftTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_segments_ShiftTemplateId_Sequence",
                table: "hr_shift_segments",
                columns: new[] { "ShiftTemplateId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_swap_requests_RequesterAssignmentId",
                table: "hr_shift_swap_requests",
                column: "RequesterAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_swap_requests_RequesterEmployeeId_Status",
                table: "hr_shift_swap_requests",
                columns: new[] { "RequesterEmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_swap_requests_TargetAssignmentId",
                table: "hr_shift_swap_requests",
                column: "TargetAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_swap_requests_TargetEmployeeId",
                table: "hr_shift_swap_requests",
                column: "TargetEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_templates_Code",
                table: "hr_shift_templates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_shift_templates_WorkCalendarId",
                table: "hr_shift_templates",
                column: "WorkCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_work_calendars_Code",
                table: "hr_work_calendars",
                column: "Code",
                unique: true);

            migrationBuilder.InsertData(
                table: "hr_work_calendars",
                columns: new[] { "Id", "Code", "Name", "TimeZoneId", "WorkingDaysMask", "HolidaysJson", "IsActive", "CreatedAt" },
                values: new object[] { new Guid("c4a10da4-d242-4d0c-907f-f705eedf0426"), "CAIRO-DEFAULT", "تقويم القاهرة الافتراضي", "Africa/Cairo", 62, "[]", true, new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE "hr_shift_assignments"
                ADD CONSTRAINT "EX_hr_shift_assignments_employee_period"
                EXCLUDE USING gist (
                    "EmployeeId" WITH =,
                    daterange("EffectiveFrom", COALESCE("EffectiveTo", 'infinity'::date), '[)') WITH &&
                ) WHERE ("Status" = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_shift_segments");

            migrationBuilder.DropTable(
                name: "hr_shift_swap_requests");

            migrationBuilder.DropTable(
                name: "hr_shift_assignments");

            migrationBuilder.DropTable(
                name: "hr_shift_templates");

            migrationBuilder.DropTable(
                name: "hr_work_calendars");
        }
    }
}
