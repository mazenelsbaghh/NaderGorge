using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSupportCommandCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_support_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByIdentity = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "live_support_guest_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SecurityStampHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedIpHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserAgentSummary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_guest_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "live_support_staff_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxActiveConversations = table.Column<int>(type: "integer", nullable: false),
                    LastAssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfiguredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_staff_configs", x => x.Id);
                    table.CheckConstraint("CK_live_support_staff_capacity", "\"MaxActiveConversations\" BETWEEN 1 AND 50");
                    table.ForeignKey(
                        name: "FK_live_support_staff_configs_users_ConfiguredByUserId",
                        column: x => x.ConfiguredByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_staff_configs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantType = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuestSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedStudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentOwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FirstStaffResponseAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_conversations", x => x.Id);
                    table.CheckConstraint("CK_live_support_conversation_identity", "(\"ParticipantType\" = 0 AND \"StudentUserId\" IS NOT NULL AND \"GuestSessionId\" IS NULL) OR (\"ParticipantType\" = 1 AND \"GuestSessionId\" IS NOT NULL AND \"StudentUserId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_live_support_conversations_live_support_conversations_Previ~",
                        column: x => x.PreviousConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_conversations_live_support_guest_sessions_Gues~",
                        column: x => x.GuestSessionId,
                        principalTable: "live_support_guest_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_conversations_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_conversations_users_CurrentOwnerUserId",
                        column: x => x.CurrentOwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_conversations_users_LinkedStudentUserId",
                        column: x => x.LinkedStudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_conversations_users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_schedule_windows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartLocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndLocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_schedule_windows", x => x.Id);
                    table.CheckConstraint("CK_live_support_schedule_day", "\"DayOfWeek\" BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_live_support_schedule_time", "\"StartLocalTime\" < \"EndLocalTime\"");
                    table.ForeignKey(
                        name: "FK_live_support_schedule_windows_live_support_staff_configs_St~",
                        column: x => x.StaffConfigId,
                        principalTable: "live_support_staff_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_support_action_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SafeRequestJson = table.Column<string>(type: "jsonb", nullable: false),
                    SafeResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AuditLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_action_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_action_executions_audit_logs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "audit_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_action_executions_live_support_conversations_C~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_action_executions_users_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_action_executions_users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndReason = table.Column<int>(type: "integer", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssignmentSequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_assignments_live_support_conversations_Convers~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_assignments_users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_assignments_users_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorGuestSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    SafeMetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_events_live_support_conversations_Conversation~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_events_live_support_guest_sessions_ActorGuestS~",
                        column: x => x.ActorGuestSessionId,
                        principalTable: "live_support_guest_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_events_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderGuestSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_messages_live_support_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "live_support_attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_messages_live_support_conversations_Conversati~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_messages_live_support_guest_sessions_SenderGue~",
                        column: x => x.SenderGuestSessionId,
                        principalTable: "live_support_guest_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_messages_users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_queue_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    DequeuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DequeueReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_queue_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_queue_entries_live_support_conversations_Conve~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedByGuestSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ratings", x => x.Id);
                    table.CheckConstraint("CK_live_support_rating_stars", "\"Stars\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_live_support_ratings_live_support_conversations_Conversatio~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ratings_live_support_guest_sessions_SubmittedB~",
                        column: x => x.SubmittedByGuestSessionId,
                        principalTable: "live_support_guest_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ratings_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_student_link_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewStudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_student_link_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_student_link_history_live_support_conversation~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_student_link_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_student_link_history_users_NewStudentUserId",
                        column: x => x.NewStudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_student_link_history_users_PreviousStudentUser~",
                        column: x => x.PreviousStudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_action_executions_AuditLogId",
                table: "live_support_action_executions",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_action_executions_ConversationId_StartedAt",
                table: "live_support_action_executions",
                columns: new[] { "ConversationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_action_executions_StaffUserId_IdempotencyKey",
                table: "live_support_action_executions",
                columns: new[] { "StaffUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_action_executions_StudentUserId_StartedAt",
                table: "live_support_action_executions",
                columns: new[] { "StudentUserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_assignments_AssignedByUserId",
                table: "live_support_assignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_assignments_ConversationId",
                table: "live_support_assignments",
                column: "ConversationId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_assignments_ConversationId_AssignmentSequence",
                table: "live_support_assignments",
                columns: new[] { "ConversationId", "AssignmentSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_assignments_StaffUserId_EndedAt_StartedAt",
                table: "live_support_assignments",
                columns: new[] { "StaffUserId", "EndedAt", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_ClosedByUserId",
                table: "live_support_conversations",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_CurrentOwnerUserId_Status",
                table: "live_support_conversations",
                columns: new[] { "CurrentOwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_GuestSessionId",
                table: "live_support_conversations",
                column: "GuestSessionId",
                unique: true,
                filter: "\"GuestSessionId\" IS NOT NULL AND \"Status\" IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_LastMessageAt",
                table: "live_support_conversations",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_LinkedStudentUserId_CreatedAt",
                table: "live_support_conversations",
                columns: new[] { "LinkedStudentUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_PreviousConversationId",
                table: "live_support_conversations",
                column: "PreviousConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_Status_QueuedAt_Id",
                table: "live_support_conversations",
                columns: new[] { "Status", "QueuedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_conversations_StudentUserId",
                table: "live_support_conversations",
                column: "StudentUserId",
                unique: true,
                filter: "\"StudentUserId\" IS NOT NULL AND \"Status\" IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_events_ActorGuestSessionId",
                table: "live_support_events",
                column: "ActorGuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_events_ActorUserId",
                table: "live_support_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_events_ConversationId_Sequence",
                table: "live_support_events",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_events_Type_OccurredAt",
                table: "live_support_events",
                columns: new[] { "Type", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_guest_sessions_ExpiresAt",
                table: "live_support_guest_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_guest_sessions_PhoneNumber_CreatedAt",
                table: "live_support_guest_sessions",
                columns: new[] { "PhoneNumber", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_guest_sessions_RevokedAt",
                table: "live_support_guest_sessions",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messages_AttachmentId",
                table: "live_support_messages",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messages_ConversationId_ClientMessageId",
                table: "live_support_messages",
                columns: new[] { "ConversationId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messages_ConversationId_SentAt_Id",
                table: "live_support_messages",
                columns: new[] { "ConversationId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messages_SenderGuestSessionId",
                table: "live_support_messages",
                column: "SenderGuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_messages_SenderUserId",
                table: "live_support_messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_queue_entries_ConversationId",
                table: "live_support_queue_entries",
                column: "ConversationId",
                unique: true,
                filter: "\"DequeuedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_queue_entries_DequeuedAt_EnteredAt_Sequence",
                table: "live_support_queue_entries",
                columns: new[] { "DequeuedAt", "EnteredAt", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ratings_ConversationId",
                table: "live_support_ratings",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ratings_SubmittedByGuestSessionId",
                table: "live_support_ratings",
                column: "SubmittedByGuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ratings_SubmittedByUserId",
                table: "live_support_ratings",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_schedule_windows_StaffConfigId_DayOfWeek_Start~",
                table: "live_support_schedule_windows",
                columns: new[] { "StaffConfigId", "DayOfWeek", "StartLocalTime", "EndLocalTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_staff_configs_ConfiguredByUserId",
                table: "live_support_staff_configs",
                column: "ConfiguredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_staff_configs_UserId",
                table: "live_support_staff_configs",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_student_link_history_ChangedByUserId",
                table: "live_support_student_link_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_student_link_history_ConversationId_ChangedAt",
                table: "live_support_student_link_history",
                columns: new[] { "ConversationId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_student_link_history_NewStudentUserId",
                table: "live_support_student_link_history",
                column: "NewStudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_student_link_history_PreviousStudentUserId",
                table: "live_support_student_link_history",
                column: "PreviousStudentUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_support_action_executions");

            migrationBuilder.DropTable(
                name: "live_support_assignments");

            migrationBuilder.DropTable(
                name: "live_support_events");

            migrationBuilder.DropTable(
                name: "live_support_messages");

            migrationBuilder.DropTable(
                name: "live_support_queue_entries");

            migrationBuilder.DropTable(
                name: "live_support_ratings");

            migrationBuilder.DropTable(
                name: "live_support_schedule_windows");

            migrationBuilder.DropTable(
                name: "live_support_student_link_history");

            migrationBuilder.DropTable(
                name: "live_support_attachments");

            migrationBuilder.DropTable(
                name: "live_support_staff_configs");

            migrationBuilder.DropTable(
                name: "live_support_conversations");

            migrationBuilder.DropTable(
                name: "live_support_guest_sessions");
        }
    }
}
