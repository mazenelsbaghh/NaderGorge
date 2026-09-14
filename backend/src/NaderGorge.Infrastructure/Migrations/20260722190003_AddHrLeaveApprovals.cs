using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrLeaveApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceCorrections_employee_profiles_EmployeeId",
                table: "AttendanceCorrections");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceCorrections_hr_attendance_sessions_AttendanceSess~",
                table: "AttendanceCorrections");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkdayClassifications_employee_profiles_EmployeeId",
                table: "WorkdayClassifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkdayClassifications",
                table: "WorkdayClassifications");

            migrationBuilder.DropIndex(
                name: "IX_WorkdayClassifications_EmployeeId",
                table: "WorkdayClassifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AttendanceCorrections",
                table: "AttendanceCorrections");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceCorrections_EmployeeId",
                table: "AttendanceCorrections");

            migrationBuilder.RenameTable(
                name: "WorkdayClassifications",
                newName: "hr_workday_classifications");

            migrationBuilder.RenameTable(
                name: "AttendanceCorrections",
                newName: "hr_attendance_corrections");

            migrationBuilder.RenameIndex(
                name: "IX_AttendanceCorrections_AttendanceSessionId",
                table: "hr_attendance_corrections",
                newName: "IX_hr_attendance_corrections_AttendanceSessionId");

            migrationBuilder.AlterColumn<string>(
                name: "SourceType",
                table: "hr_workday_classifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "hr_attendance_corrections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EvidenceReference",
                table: "hr_attendance_corrections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DecisionReason",
                table: "hr_attendance_corrections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // PostgreSQL does not provide an implicit text -> jsonb cast for ALTER COLUMN.
            // Existing correction snapshots are JSON documents, so preserve them explicitly.
            migrationBuilder.Sql(
                "ALTER TABLE hr_attendance_corrections " +
                "ALTER COLUMN \"BeforeJson\" TYPE jsonb USING \"BeforeJson\"::jsonb;");
            migrationBuilder.Sql(
                "ALTER TABLE hr_attendance_corrections " +
                "ALTER COLUMN \"AppliedJson\" TYPE jsonb USING \"AppliedJson\"::jsonb;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_hr_workday_classifications",
                table: "hr_workday_classifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_hr_attendance_corrections",
                table: "hr_attendance_corrections",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "hr_approval_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_approval_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_approval_delegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_approval_delegations", x => x.Id);
                    table.CheckConstraint("CK_hr_approval_delegation_dates", "\"EndsAt\" > \"StartsAt\"");
                });

            migrationBuilder.CreateTable(
                name: "hr_leave_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAttachment = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsHalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_approval_definition_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApproverKind = table.Column<int>(type: "integer", nullable: false),
                    Permission = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SpecificUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SlaMinutes = table.Column<int>(type: "integer", nullable: false),
                    EscalationPermission = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_approval_definition_steps", x => x.Id);
                    table.CheckConstraint("CK_hr_approval_step_sla", "\"SlaMinutes\" > 0");
                    table.ForeignKey(
                        name: "FK_hr_approval_definition_steps_hr_approval_definitions_Approv~",
                        column: x => x.ApprovalDefinitionId,
                        principalTable: "hr_approval_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_approval_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_approval_instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_approval_instances_employee_profiles_RequesterEmployeeId",
                        column: x => x.RequesterEmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_approval_instances_hr_approval_definitions_ApprovalDefin~",
                        column: x => x.ApprovalDefinitionId,
                        principalTable: "hr_approval_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_leave_balances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Granted = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Carried = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Used = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_balances", x => x.Id);
                    table.CheckConstraint("CK_hr_leave_balance_nonnegative", "\"Reserved\" >= 0 AND \"Used\" >= 0");
                    table.ForeignKey(
                        name: "FK_hr_leave_balances_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_leave_balances_hr_leave_types_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "hr_leave_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_leave_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnualEntitlement = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MaximumCarryover = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    AllowNegativeBalance = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    WorkCalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_policies", x => x.Id);
                    table.CheckConstraint("CK_hr_leave_policy_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_hr_leave_policies_hr_leave_types_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "hr_leave_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_leave_policies_hr_work_calendars_WorkCalendarId",
                        column: x => x.WorkCalendarId,
                        principalTable: "hr_work_calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_approval_step_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalDefinitionStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    OriginalApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActingUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DelegationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_approval_step_instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_approval_step_instances_hr_approval_definition_steps_App~",
                        column: x => x.ApprovalDefinitionStepId,
                        principalTable: "hr_approval_definition_steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_approval_step_instances_hr_approval_instances_ApprovalIn~",
                        column: x => x.ApprovalInstanceId,
                        principalTable: "hr_approval_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_leave_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DayFraction = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    Workdays = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ReservedAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AttachmentReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ApprovalInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_requests", x => x.Id);
                    table.CheckConstraint("CK_hr_leave_request_dates", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("CK_hr_leave_request_fraction", "\"DayFraction\" > 0 AND \"DayFraction\" <= 1");
                    table.ForeignKey(
                        name: "FK_hr_leave_requests_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_leave_requests_hr_approval_instances_ApprovalInstanceId",
                        column: x => x.ApprovalInstanceId,
                        principalTable: "hr_approval_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_leave_requests_hr_leave_types_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "hr_leave_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_leave_ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_leave_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_leave_ledger_entries_hr_leave_balances_LeaveBalanceId",
                        column: x => x.LeaveBalanceId,
                        principalTable: "hr_leave_balances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_workday_classifications_EmployeeId_WorkDate",
                table: "hr_workday_classifications",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_attendance_corrections_EmployeeId_State",
                table: "hr_attendance_corrections",
                columns: new[] { "EmployeeId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_definition_steps_ApprovalDefinitionId_Order",
                table: "hr_approval_definition_steps",
                columns: new[] { "ApprovalDefinitionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_definitions_RequestType_Version",
                table: "hr_approval_definitions",
                columns: new[] { "RequestType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_delegations_PrincipalUserId_DelegateUserId_Scop~",
                table: "hr_approval_delegations",
                columns: new[] { "PrincipalUserId", "DelegateUserId", "Scope", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_instances_ApprovalDefinitionId",
                table: "hr_approval_instances",
                column: "ApprovalDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_instances_RequesterEmployeeId",
                table: "hr_approval_instances",
                column: "RequesterEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_instances_RequestType_RequestId",
                table: "hr_approval_instances",
                columns: new[] { "RequestType", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_instances_State_CurrentStepOrder",
                table: "hr_approval_instances",
                columns: new[] { "State", "CurrentStepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_step_instances_ApprovalDefinitionStepId",
                table: "hr_approval_step_instances",
                column: "ApprovalDefinitionStepId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_step_instances_ApprovalInstanceId_Order",
                table: "hr_approval_step_instances",
                columns: new[] { "ApprovalInstanceId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_approval_step_instances_State_DueAt",
                table: "hr_approval_step_instances",
                columns: new[] { "State", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_balances_EmployeeId_LeaveTypeId_Year",
                table: "hr_leave_balances",
                columns: new[] { "EmployeeId", "LeaveTypeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_balances_LeaveTypeId",
                table: "hr_leave_balances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_ledger_entries_LeaveBalanceId",
                table: "hr_leave_ledger_entries",
                column: "LeaveBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_ledger_entries_SourceType_SourceId_EntryType",
                table: "hr_leave_ledger_entries",
                columns: new[] { "SourceType", "SourceId", "EntryType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_policies_LeaveTypeId_EffectiveFrom",
                table: "hr_leave_policies",
                columns: new[] { "LeaveTypeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_policies_WorkCalendarId",
                table: "hr_leave_policies",
                column: "WorkCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_requests_ApprovalInstanceId",
                table: "hr_leave_requests",
                column: "ApprovalInstanceId",
                unique: true,
                filter: "\"ApprovalInstanceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_requests_EmployeeId_StartDate_EndDate",
                table: "hr_leave_requests",
                columns: new[] { "EmployeeId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_requests_LeaveTypeId",
                table: "hr_leave_requests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_leave_types_Code",
                table: "hr_leave_types",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_hr_attendance_corrections_employee_profiles_EmployeeId",
                table: "hr_attendance_corrections",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hr_attendance_corrections_hr_attendance_sessions_Attendance~",
                table: "hr_attendance_corrections",
                column: "AttendanceSessionId",
                principalTable: "hr_attendance_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hr_workday_classifications_employee_profiles_EmployeeId",
                table: "hr_workday_classifications",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hr_attendance_corrections_employee_profiles_EmployeeId",
                table: "hr_attendance_corrections");

            migrationBuilder.DropForeignKey(
                name: "FK_hr_attendance_corrections_hr_attendance_sessions_Attendance~",
                table: "hr_attendance_corrections");

            migrationBuilder.DropForeignKey(
                name: "FK_hr_workday_classifications_employee_profiles_EmployeeId",
                table: "hr_workday_classifications");

            migrationBuilder.DropTable(
                name: "hr_approval_delegations");

            migrationBuilder.DropTable(
                name: "hr_approval_step_instances");

            migrationBuilder.DropTable(
                name: "hr_leave_ledger_entries");

            migrationBuilder.DropTable(
                name: "hr_leave_policies");

            migrationBuilder.DropTable(
                name: "hr_leave_requests");

            migrationBuilder.DropTable(
                name: "hr_approval_definition_steps");

            migrationBuilder.DropTable(
                name: "hr_leave_balances");

            migrationBuilder.DropTable(
                name: "hr_approval_instances");

            migrationBuilder.DropTable(
                name: "hr_leave_types");

            migrationBuilder.DropTable(
                name: "hr_approval_definitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_hr_workday_classifications",
                table: "hr_workday_classifications");

            migrationBuilder.DropIndex(
                name: "IX_hr_workday_classifications_EmployeeId_WorkDate",
                table: "hr_workday_classifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_hr_attendance_corrections",
                table: "hr_attendance_corrections");

            migrationBuilder.DropIndex(
                name: "IX_hr_attendance_corrections_EmployeeId_State",
                table: "hr_attendance_corrections");

            migrationBuilder.RenameTable(
                name: "hr_workday_classifications",
                newName: "WorkdayClassifications");

            migrationBuilder.RenameTable(
                name: "hr_attendance_corrections",
                newName: "AttendanceCorrections");

            migrationBuilder.RenameIndex(
                name: "IX_hr_attendance_corrections_AttendanceSessionId",
                table: "AttendanceCorrections",
                newName: "IX_AttendanceCorrections_AttendanceSessionId");

            migrationBuilder.AlterColumn<string>(
                name: "SourceType",
                table: "WorkdayClassifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "AttendanceCorrections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "EvidenceReference",
                table: "AttendanceCorrections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DecisionReason",
                table: "AttendanceCorrections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"AttendanceCorrections\" " +
                "ALTER COLUMN \"BeforeJson\" TYPE text USING \"BeforeJson\"::text;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AttendanceCorrections\" " +
                "ALTER COLUMN \"AppliedJson\" TYPE text USING \"AppliedJson\"::text;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkdayClassifications",
                table: "WorkdayClassifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AttendanceCorrections",
                table: "AttendanceCorrections",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkdayClassifications_EmployeeId",
                table: "WorkdayClassifications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_EmployeeId",
                table: "AttendanceCorrections",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceCorrections_employee_profiles_EmployeeId",
                table: "AttendanceCorrections",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceCorrections_hr_attendance_sessions_AttendanceSess~",
                table: "AttendanceCorrections",
                column: "AttendanceSessionId",
                principalTable: "hr_attendance_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkdayClassifications_employee_profiles_EmployeeId",
                table: "WorkdayClassifications",
                column: "EmployeeId",
                principalTable: "employee_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
