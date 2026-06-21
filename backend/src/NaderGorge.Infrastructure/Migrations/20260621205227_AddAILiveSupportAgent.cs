using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAILiveSupportAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_support_ai_knowledge_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_knowledge_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_ai_knowledge_entries_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_policy_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SystemInstructions = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    ReadableDataKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    ActionKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    LookupKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    VerificationQuestionKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    VerificationRequiredCorrect = table.Column<int>(type: "integer", nullable: false),
                    VerificationMaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    PendingActionExpirySeconds = table.Column<int>(type: "integer", nullable: false),
                    InactivityMinutes = table.Column<int>(type: "integer", nullable: false),
                    InactivityWarningGraceSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_policy_versions", x => x.Id);
                    table.CheckConstraint("CK_live_support_ai_policy_action_expiry", "\"PendingActionExpirySeconds\" BETWEEN 30 AND 900");
                    table.CheckConstraint("CK_live_support_ai_policy_inactivity", "\"InactivityMinutes\" BETWEEN 5 AND 1440 AND \"InactivityWarningGraceSeconds\" BETWEEN 30 AND 600");
                    table.CheckConstraint("CK_live_support_ai_policy_verification", "\"VerificationRequiredCorrect\" >= 1 AND \"VerificationMaxAttempts\" BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_live_support_ai_policy_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_policy_versions_users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_knowledge_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    SourceLabel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SearchText = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_knowledge_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_ai_knowledge_revisions_live_support_ai_knowled~",
                        column: x => x.EntryId,
                        principalTable: "live_support_ai_knowledge_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_knowledge_revisions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_knowledge_revisions_users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_conversation_states",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedStudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastParticipantActivityAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InactivityWarningSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AutoCloseAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HandoffReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HandoffSafeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HandedOffAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolutionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SafeSummaryJson = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_conversation_states", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_live_support_ai_conversation_states_live_support_ai_policy_~",
                        column: x => x.PolicyVersionId,
                        principalTable: "live_support_ai_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_conversation_states_live_support_conversati~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_conversation_states_users_VerifiedStudentUs~",
                        column: x => x.VerifiedStudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_turns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedConversationVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecisionType = table.Column<int>(type: "integer", nullable: true),
                    OutputMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextCategoryKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    KnowledgeRevisionIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderResponseId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    OutputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SafeFailureDetail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_turns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_ai_turns_live_support_ai_policy_versions_Polic~",
                        column: x => x.PolicyVersionId,
                        principalTable: "live_support_ai_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_turns_live_support_conversations_Conversati~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_turns_live_support_messages_OutputMessageId",
                        column: x => x.OutputMessageId,
                        principalTable: "live_support_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_turns_live_support_messages_SourceMessageId",
                        column: x => x.SourceMessageId,
                        principalTable: "live_support_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_verification_policy_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceFieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ComparisonMode = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_verification_policy_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_ai_verification_policy_questions_live_support_~",
                        column: x => x.PolicyVersionId,
                        principalTable: "live_support_ai_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_verification_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateStudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LookupKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LookupValueHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SelectedQuestionKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    RequiredCorrect = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_verification_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_ai_verification_sessions_live_support_ai_polic~",
                        column: x => x.PolicyVersionId,
                        principalTable: "live_support_ai_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_verification_sessions_live_support_conversa~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_verification_sessions_users_CandidateStuden~",
                        column: x => x.CandidateStudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_policy_knowledge_revisions",
                columns: table => new
                {
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeRevisionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_policy_knowledge_revisions", x => new { x.PolicyVersionId, x.KnowledgeRevisionId });
                    table.ForeignKey(
                        name: "FK_live_support_ai_policy_knowledge_revisions_live_support_ai_~",
                        column: x => x.KnowledgeRevisionId,
                        principalTable: "live_support_ai_knowledge_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_policy_knowledge_revisions_live_support_ai~1",
                        column: x => x.PolicyVersionId,
                        principalTable: "live_support_ai_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_pending_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SafeProposalJson = table.Column<string>(type: "jsonb", nullable: false),
                    EncryptedPayload = table.Column<byte[]>(type: "bytea", nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfirmationNonceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedByGuestSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_pending_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_ai_pending_actions_live_support_action_executi~",
                        column: x => x.ActionExecutionId,
                        principalTable: "live_support_action_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_pending_actions_live_support_ai_policy_vers~",
                        column: x => x.PolicyVersionId,
                        principalTable: "live_support_ai_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_pending_actions_live_support_ai_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "live_support_ai_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_pending_actions_live_support_conversations_~",
                        column: x => x.ConversationId,
                        principalTable: "live_support_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_pending_actions_live_support_guest_sessions~",
                        column: x => x.ConfirmedByGuestSessionId,
                        principalTable: "live_support_guest_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_pending_actions_users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_support_ai_pending_actions_users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_support_ai_verification_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    OutcomeCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_support_ai_verification_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_support_ai_verification_attempts_live_support_ai_verif~",
                        column: x => x.SessionId,
                        principalTable: "live_support_ai_verification_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_conversation_states_Mode_AutoCloseAt",
                table: "live_support_ai_conversation_states",
                columns: new[] { "Mode", "AutoCloseAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_conversation_states_Mode_LastParticipantAct~",
                table: "live_support_ai_conversation_states",
                columns: new[] { "Mode", "LastParticipantActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_conversation_states_PolicyVersionId",
                table: "live_support_ai_conversation_states",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_conversation_states_VerifiedStudentUserId",
                table: "live_support_ai_conversation_states",
                column: "VerifiedStudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_knowledge_entries_CreatedByUserId",
                table: "live_support_ai_knowledge_entries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_knowledge_revisions_CreatedByUserId",
                table: "live_support_ai_knowledge_revisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_knowledge_revisions_EntryId_RevisionNumber",
                table: "live_support_ai_knowledge_revisions",
                columns: new[] { "EntryId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_knowledge_revisions_PublishedByUserId",
                table: "live_support_ai_knowledge_revisions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_ActionExecutionId",
                table: "live_support_ai_pending_actions",
                column: "ActionExecutionId",
                unique: true,
                filter: "\"ActionExecutionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_ConfirmedByGuestSessionId",
                table: "live_support_ai_pending_actions",
                column: "ConfirmedByGuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_ConfirmedByUserId",
                table: "live_support_ai_pending_actions",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_ConversationId_Status",
                table: "live_support_ai_pending_actions",
                columns: new[] { "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_IdempotencyKey",
                table: "live_support_ai_pending_actions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_PolicyVersionId",
                table: "live_support_ai_pending_actions",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_StudentUserId",
                table: "live_support_ai_pending_actions",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_TurnId",
                table: "live_support_ai_pending_actions",
                column: "TurnId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_policy_knowledge_revisions_KnowledgeRevisio~",
                table: "live_support_ai_policy_knowledge_revisions",
                column: "KnowledgeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_policy_versions_CreatedByUserId",
                table: "live_support_ai_policy_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_policy_versions_IsEnabled",
                table: "live_support_ai_policy_versions",
                column: "IsEnabled",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsEnabled\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_policy_versions_PublishedByUserId",
                table: "live_support_ai_policy_versions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_policy_versions_VersionNumber",
                table: "live_support_ai_policy_versions",
                column: "VersionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_turns_ConversationId_QueuedAt",
                table: "live_support_ai_turns",
                columns: new[] { "ConversationId", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_turns_OutputMessageId",
                table: "live_support_ai_turns",
                column: "OutputMessageId",
                unique: true,
                filter: "\"OutputMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_turns_PolicyVersionId",
                table: "live_support_ai_turns",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_turns_SourceMessageId",
                table: "live_support_ai_turns",
                column: "SourceMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_turns_Status_QueuedAt",
                table: "live_support_ai_turns",
                columns: new[] { "Status", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_attempts_SessionId_AttemptNumb~",
                table: "live_support_ai_verification_attempts",
                columns: new[] { "SessionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_policy_questions_PolicyVersio~1",
                table: "live_support_ai_verification_policy_questions",
                columns: new[] { "PolicyVersionId", "QuestionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_policy_questions_PolicyVersion~",
                table: "live_support_ai_verification_policy_questions",
                columns: new[] { "PolicyVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_sessions_CandidateStudentUserId",
                table: "live_support_ai_verification_sessions",
                column: "CandidateStudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_sessions_ConversationId",
                table: "live_support_ai_verification_sessions",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_sessions_PolicyVersionId",
                table: "live_support_ai_verification_sessions",
                column: "PolicyVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_support_ai_conversation_states");

            migrationBuilder.DropTable(
                name: "live_support_ai_pending_actions");

            migrationBuilder.DropTable(
                name: "live_support_ai_policy_knowledge_revisions");

            migrationBuilder.DropTable(
                name: "live_support_ai_verification_attempts");

            migrationBuilder.DropTable(
                name: "live_support_ai_verification_policy_questions");

            migrationBuilder.DropTable(
                name: "live_support_ai_turns");

            migrationBuilder.DropTable(
                name: "live_support_ai_knowledge_revisions");

            migrationBuilder.DropTable(
                name: "live_support_ai_verification_sessions");

            migrationBuilder.DropTable(
                name: "live_support_ai_knowledge_entries");

            migrationBuilder.DropTable(
                name: "live_support_ai_policy_versions");
        }
    }
}
