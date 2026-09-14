using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustedAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_attendance_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RadiusMeters = table.Column<int>(type: "integer", nullable: false),
                    MaximumAccuracyMeters = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_attendance_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_attendance_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClockedInAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClockedOutAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    LateMinutes = table.Column<int>(type: "integer", nullable: false),
                    EarlyLeaveMinutes = table.Column<int>(type: "integer", nullable: false),
                    OvertimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    WorkedMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_attendance_sessions", x => x.Id);
                    table.CheckConstraint("CK_hr_attendance_session_times", "\"ClockedOutAt\" IS NULL OR \"ClockedOutAt\" > \"ClockedInAt\"");
                    table.ForeignKey(
                        name: "FK_hr_attendance_sessions_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_attendance_sessions_hr_shift_assignments_ShiftAssignment~",
                        column: x => x.ShiftAssignmentId,
                        principalTable: "hr_shift_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_trusted_attendance_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_trusted_attendance_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_trusted_attendance_devices_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_trusted_attendance_devices_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_attendance_policy_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendancePolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShiftTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_attendance_policy_assignments", x => x.Id);
                    table.CheckConstraint("CK_hr_attendance_policy_assignment_target", "(CASE WHEN \"EmployeeId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"ShiftTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_hr_attendance_policy_assignments_employee_profiles_Employee~",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_attendance_policy_assignments_hr_attendance_policies_Att~",
                        column: x => x.AttendancePolicyId,
                        principalTable: "hr_attendance_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_attendance_policy_assignments_hr_shift_templates_ShiftTe~",
                        column: x => x.ShiftTemplateId,
                        principalTable: "hr_shift_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_attendance_policy_exceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowRemote = table.Column<bool>(type: "boolean", nullable: false),
                    OverridePolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_attendance_policy_exceptions", x => x.Id);
                    table.CheckConstraint("CK_hr_attendance_policy_exception_dates", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_hr_attendance_policy_exceptions_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_attendance_policy_exceptions_hr_attendance_policies_Over~",
                        column: x => x.OverridePolicyId,
                        principalTable: "hr_attendance_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_attendance_policy_exceptions_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_attendance_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    DecisionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AttendancePolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendanceSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_attendance_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_attendance_attempts_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_attendance_attempts_hr_attendance_policies_AttendancePol~",
                        column: x => x.AttendancePolicyId,
                        principalTable: "hr_attendance_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_attendance_attempts_hr_attendance_sessions_AttendanceSes~",
                        column: x => x.AttendanceSessionId,
                        principalTable: "hr_attendance_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_attendance_breaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_attendance_breaks", x => x.Id);
                    table.CheckConstraint("CK_hr_attendance_break_times", "\"EndedAt\" IS NULL OR \"EndedAt\" > \"StartedAt\"");
                    table.ForeignKey(
                        name: "FK_hr_attendance_breaks_hr_attendance_sessions_AttendanceSessi~",
                        column: x => x.AttendanceSessionId,
                        principalTable: "hr_attendance_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_attempts_AttendancePolicyId",
                table: "hr_attendance_attempts",
                column: "AttendancePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_attempts_AttendanceSessionId",
                table: "hr_attendance_attempts",
                column: "AttendanceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_attempts_EmployeeId_EventType_IdempotencyKey",
                table: "hr_attendance_attempts",
                columns: new[] { "EmployeeId", "EventType", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_attempts_EmployeeId_OccurredAt",
                table: "hr_attendance_attempts",
                columns: new[] { "EmployeeId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_breaks_AttendanceSessionId",
                table: "hr_attendance_breaks",
                column: "AttendanceSessionId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_policies_Code",
                table: "hr_attendance_policies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_policy_assignments_AttendancePolicyId",
                table: "hr_attendance_policy_assignments",
                column: "AttendancePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_policy_assignments_EmployeeId_EffectiveFrom",
                table: "hr_attendance_policy_assignments",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_policy_assignments_ShiftTemplateId_EffectiveF~",
                table: "hr_attendance_policy_assignments",
                columns: new[] { "ShiftTemplateId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_policy_exceptions_ApprovedByUserId",
                table: "hr_attendance_policy_exceptions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_policy_exceptions_EmployeeId_StartsAt_EndsAt",
                table: "hr_attendance_policy_exceptions",
                columns: new[] { "EmployeeId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_policy_exceptions_OverridePolicyId",
                table: "hr_attendance_policy_exceptions",
                column: "OverridePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_sessions_EmployeeId",
                table: "hr_attendance_sessions",
                column: "EmployeeId",
                unique: true,
                filter: "\"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_sessions_EmployeeId_WorkDate",
                table: "hr_attendance_sessions",
                columns: new[] { "EmployeeId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_sessions_ShiftAssignmentId",
                table: "hr_attendance_sessions",
                column: "ShiftAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_trusted_attendance_devices_ApprovedByUserId",
                table: "hr_trusted_attendance_devices",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_trusted_attendance_devices_EmployeeId_TokenHash",
                table: "hr_trusted_attendance_devices",
                columns: new[] { "EmployeeId", "TokenHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_attendance_attempts");

            migrationBuilder.DropTable(
                name: "hr_attendance_breaks");

            migrationBuilder.DropTable(
                name: "hr_attendance_policy_assignments");

            migrationBuilder.DropTable(
                name: "hr_attendance_policy_exceptions");

            migrationBuilder.DropTable(
                name: "hr_trusted_attendance_devices");

            migrationBuilder.DropTable(
                name: "hr_attendance_sessions");

            migrationBuilder.DropTable(
                name: "hr_attendance_policies");
        }
    }
}
