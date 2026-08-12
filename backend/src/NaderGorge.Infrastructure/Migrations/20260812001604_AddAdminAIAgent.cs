using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAIAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_ai_capability_baselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ManifestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SafeManifestJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceRevision = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuntimeInventoryHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FrontendInventoryHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SupportedReadCount = table.Column<int>(type: "integer", nullable: false),
                    SupportedActionCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_capability_baselines", x => x.Id);
                    table.CheckConstraint("ck_admin_ai_baseline_counts", "\"SupportedReadCount\" >= 0 AND \"SupportedActionCount\" >= 0 AND \"ExcludedCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_admin_ai_capability_baselines_users_ApprovedByAdminUserId",
                        column: x => x.ApprovedByAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_conversations", x => x.Id);
                    table.CheckConstraint("ck_admin_ai_conversation_version", "\"LastSequence\" >= 0 AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_admin_ai_conversations_users_OwnerAdminUserId",
                        column: x => x.OwnerAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_sensitive_policy_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PolicyHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SafeRulesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_sensitive_policy_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_sensitive_policy_versions_users_ApprovedByAdminUse~",
                        column: x => x.ApprovedByAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_turns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityBaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SensitiveDataPolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedConversationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedSecurityVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentStepNumber = table.Column<int>(type: "integer", nullable: false),
                    ReadInvocationCount = table.Column<int>(type: "integer", nullable: false),
                    RedactedContextBytes = table.Column<int>(type: "integer", nullable: false),
                    CancellationRequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CallbackIdempotencyDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderResponseId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    OutputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SafeFailureDetail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_turns", x => x.Id);
                    table.CheckConstraint("ck_admin_ai_turn_budgets", "\"CurrentStepNumber\" BETWEEN 0 AND 3 AND \"ReadInvocationCount\" BETWEEN 0 AND 6 AND \"RedactedContextBytes\" BETWEEN 0 AND 65536 AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_admin_ai_turns_admin_ai_capability_baselines_CapabilityBase~",
                        column: x => x.CapabilityBaselineId,
                        principalTable: "admin_ai_capability_baselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_turns_admin_ai_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "admin_ai_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_turns_admin_ai_sensitive_policy_versions_Sensitive~",
                        column: x => x.SensitiveDataPolicyVersionId,
                        principalTable: "admin_ai_sensitive_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_turns_users_ActorAdminUserId",
                        column: x => x.ActorAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_action_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityBaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SensitiveDataPolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CapabilityVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PrimaryRisk = table.Column<int>(type: "integer", nullable: false),
                    RiskFlagsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConfirmationType = table.Column<int>(type: "integer", nullable: false),
                    SafeTargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SafeTargetReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProtectedNormalizedPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    PayloadHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StateFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SafeCurrentStateJson = table.Column<string>(type: "jsonb", nullable: false),
                    SafeRequestedStateJson = table.Column<string>(type: "jsonb", nullable: false),
                    SafeEffectJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    BulkSemanticsJson = table.Column<string>(type: "jsonb", nullable: true),
                    SecureInputGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InvalidatedReasonCode = table.Column<string>(type: "text", nullable: true),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_action_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_proposals_admin_ai_capability_baselines_Cap~",
                        column: x => x.CapabilityBaselineId,
                        principalTable: "admin_ai_capability_baselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_proposals_admin_ai_conversations_Conversati~",
                        column: x => x.ConversationId,
                        principalTable: "admin_ai_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_proposals_admin_ai_sensitive_policy_version~",
                        column: x => x.SensitiveDataPolicyVersionId,
                        principalTable: "admin_ai_sensitive_policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_proposals_admin_ai_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "admin_ai_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_proposals_users_ActorAdminUserId",
                        column: x => x.ActorAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    StructuredContentJson = table.Column<string>(type: "jsonb", nullable: true),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_messages_admin_ai_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "admin_ai_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_messages_admin_ai_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "admin_ai_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_turn_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecisionType = table.Column<int>(type: "integer", nullable: true),
                    CanonicalDecisionHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ExpectedTurnVersion = table.Column<long>(type: "bigint", nullable: false),
                    ToolCallsRequested = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    ProviderResponseId = table.Column<string>(type: "text", nullable: true),
                    InputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    OutputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: true),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    CallbackStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CallbackAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextCallbackAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_turn_steps", x => x.Id);
                    table.CheckConstraint("ck_admin_ai_step_bounds", "\"StepNumber\" BETWEEN 1 AND 3 AND \"ToolCallsRequested\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_admin_ai_turn_steps_admin_ai_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "admin_ai_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_action_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityKey = table.Column<string>(type: "text", nullable: false),
                    CapabilityVersion = table.Column<string>(type: "text", nullable: false),
                    IdempotencyDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    AuthoritativeOperation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SafeResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    AffectedCount = table.Column<int>(type: "integer", nullable: true),
                    SucceededCount = table.Column<int>(type: "integer", nullable: true),
                    SkippedCount = table.Column<int>(type: "integer", nullable: true),
                    FailedCount = table.Column<int>(type: "integer", nullable: true),
                    RefreshScopesJson = table.Column<string>(type: "jsonb", nullable: false),
                    OriginalAuditLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalOperationId = table.Column<string>(type: "text", nullable: true),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_action_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_executions_admin_ai_action_proposals_Propos~",
                        column: x => x.ProposalId,
                        principalTable: "admin_ai_action_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_executions_audit_logs_OriginalAuditLogId",
                        column: x => x.OriginalAuditLogId,
                        principalTable: "audit_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_executions_users_ActorAdminUserId",
                        column: x => x.ActorAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_confirmation_challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhraseDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ChallengeVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_confirmation_challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_confirmation_challenges_admin_ai_action_proposals_~",
                        column: x => x.ProposalId,
                        principalTable: "admin_ai_action_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_secure_input_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ProtectedPayload = table.Column<byte[]>(type: "bytea", nullable: true),
                    PayloadHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    SafeMetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PurgedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_secure_input_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_secure_input_grants_admin_ai_action_proposals_Prop~",
                        column: x => x.ProposalId,
                        principalTable: "admin_ai_action_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_secure_input_grants_users_ActorAdminUserId",
                        column: x => x.ActorAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_read_invocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvocationSequence = table.Column<int>(type: "integer", nullable: false),
                    CapabilityKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CapabilityVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SafeInputJson = table.Column<string>(type: "jsonb", nullable: false),
                    InputHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SafeScopeJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    IsTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    DataAsOf = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SafeEvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProtectedResult = table.Column<byte[]>(type: "bytea", nullable: true),
                    ProtectedResultHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ProtectedResultExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_read_invocations", x => x.Id);
                    table.CheckConstraint("ck_admin_ai_read_bounds", "\"InvocationSequence\" BETWEEN 1 AND 6 AND \"ResultCount\" >= 0 AND \"LatencyMs\" >= 0");
                    table.ForeignKey(
                        name: "FK_admin_ai_read_invocations_admin_ai_turn_steps_TurnStepId",
                        column: x => x.TurnStepId,
                        principalTable: "admin_ai_turn_steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_read_invocations_admin_ai_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "admin_ai_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_action_execution_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemSequence = table.Column<int>(type: "integer", nullable: false),
                    SafeItemReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemReferenceHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SafeResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_action_execution_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_action_execution_items_admin_ai_action_executions_~",
                        column: x => x.ExecutionId,
                        principalTable: "admin_ai_action_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_ai_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ActorAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadInvocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapabilityKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SafeTargetReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SafeEvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvidenceHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddressHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ai_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_ai_audit_events_admin_ai_action_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "admin_ai_action_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_audit_events_admin_ai_action_proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "admin_ai_action_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_audit_events_admin_ai_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "admin_ai_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_audit_events_admin_ai_read_invocations_ReadInvocat~",
                        column: x => x.ReadInvocationId,
                        principalTable: "admin_ai_read_invocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_audit_events_admin_ai_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "admin_ai_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_ai_audit_events_users_ActorAdminUserId",
                        column: x => x.ActorAdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_execution_items_ExecutionId_ItemReferenceHa~",
                table: "admin_ai_action_execution_items",
                columns: new[] { "ExecutionId", "ItemReferenceHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_execution_items_ExecutionId_ItemSequence",
                table: "admin_ai_action_execution_items",
                columns: new[] { "ExecutionId", "ItemSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_executions_ActorAdminUserId_IdempotencyDige~",
                table: "admin_ai_action_executions",
                columns: new[] { "ActorAdminUserId", "IdempotencyDigest" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_executions_OriginalAuditLogId",
                table: "admin_ai_action_executions",
                column: "OriginalAuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_executions_ProposalId",
                table: "admin_ai_action_executions",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_proposals_ActorAdminUserId_Status_ExpiresAt",
                table: "admin_ai_action_proposals",
                columns: new[] { "ActorAdminUserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_proposals_CapabilityBaselineId",
                table: "admin_ai_action_proposals",
                column: "CapabilityBaselineId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_proposals_ConversationId_CreatedAt",
                table: "admin_ai_action_proposals",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_proposals_SensitiveDataPolicyVersionId",
                table: "admin_ai_action_proposals",
                column: "SensitiveDataPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_action_proposals_TurnId",
                table: "admin_ai_action_proposals",
                column: "TurnId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_audit_events_ActorAdminUserId_OccurredAt",
                table: "admin_ai_audit_events",
                columns: new[] { "ActorAdminUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_audit_events_ConversationId_OccurredAt_Id",
                table: "admin_ai_audit_events",
                columns: new[] { "ConversationId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_audit_events_ExecutionId_OccurredAt",
                table: "admin_ai_audit_events",
                columns: new[] { "ExecutionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_audit_events_ProposalId_OccurredAt",
                table: "admin_ai_audit_events",
                columns: new[] { "ProposalId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_audit_events_ReadInvocationId",
                table: "admin_ai_audit_events",
                column: "ReadInvocationId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_audit_events_TurnId",
                table: "admin_ai_audit_events",
                column: "TurnId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_capability_baselines_ApprovedByAdminUserId",
                table: "admin_ai_capability_baselines",
                column: "ApprovedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_capability_baselines_ManifestHash",
                table: "admin_ai_capability_baselines",
                column: "ManifestHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_capability_baselines_Status",
                table: "admin_ai_capability_baselines",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_capability_baselines_Version",
                table: "admin_ai_capability_baselines",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_confirmation_challenges_ProposalId",
                table: "admin_ai_confirmation_challenges",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_conversations_OwnerAdminUserId_Status_LastActivity~",
                table: "admin_ai_conversations",
                columns: new[] { "OwnerAdminUserId", "Status", "LastActivityAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_messages_ConversationId_Sequence",
                table: "admin_ai_messages",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_messages_TurnId",
                table: "admin_ai_messages",
                column: "TurnId",
                unique: true,
                filter: "\"TurnId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_read_invocations_CapabilityKey_CreatedAt",
                table: "admin_ai_read_invocations",
                columns: new[] { "CapabilityKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_read_invocations_TurnId_InvocationSequence",
                table: "admin_ai_read_invocations",
                columns: new[] { "TurnId", "InvocationSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_read_invocations_TurnStepId",
                table: "admin_ai_read_invocations",
                column: "TurnStepId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_secure_input_grants_ActorAdminUserId",
                table: "admin_ai_secure_input_grants",
                column: "ActorAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_secure_input_grants_ProposalId",
                table: "admin_ai_secure_input_grants",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_secure_input_grants_TokenDigest",
                table: "admin_ai_secure_input_grants",
                column: "TokenDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_sensitive_policy_versions_ApprovedByAdminUserId",
                table: "admin_ai_sensitive_policy_versions",
                column: "ApprovedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_sensitive_policy_versions_PolicyHash",
                table: "admin_ai_sensitive_policy_versions",
                column: "PolicyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_sensitive_policy_versions_Status",
                table: "admin_ai_sensitive_policy_versions",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_sensitive_policy_versions_Version",
                table: "admin_ai_sensitive_policy_versions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turn_steps_TurnId_StepNumber",
                table: "admin_ai_turn_steps",
                columns: new[] { "TurnId", "StepNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turns_ActorAdminUserId_Status",
                table: "admin_ai_turns",
                columns: new[] { "ActorAdminUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turns_CallbackIdempotencyDigest",
                table: "admin_ai_turns",
                column: "CallbackIdempotencyDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turns_CapabilityBaselineId",
                table: "admin_ai_turns",
                column: "CapabilityBaselineId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turns_ConversationId_QueuedAt",
                table: "admin_ai_turns",
                columns: new[] { "ConversationId", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turns_SensitiveDataPolicyVersionId",
                table: "admin_ai_turns",
                column: "SensitiveDataPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turns_SourceMessageId",
                table: "admin_ai_turns",
                column: "SourceMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_ai_turns_Status_QueuedAt",
                table: "admin_ai_turns",
                columns: new[] { "Status", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_ai_action_execution_items");

            migrationBuilder.DropTable(
                name: "admin_ai_audit_events");

            migrationBuilder.DropTable(
                name: "admin_ai_confirmation_challenges");

            migrationBuilder.DropTable(
                name: "admin_ai_messages");

            migrationBuilder.DropTable(
                name: "admin_ai_secure_input_grants");

            migrationBuilder.DropTable(
                name: "admin_ai_action_executions");

            migrationBuilder.DropTable(
                name: "admin_ai_read_invocations");

            migrationBuilder.DropTable(
                name: "admin_ai_action_proposals");

            migrationBuilder.DropTable(
                name: "admin_ai_turn_steps");

            migrationBuilder.DropTable(
                name: "admin_ai_turns");

            migrationBuilder.DropTable(
                name: "admin_ai_capability_baselines");

            migrationBuilder.DropTable(
                name: "admin_ai_conversations");

            migrationBuilder.DropTable(
                name: "admin_ai_sensitive_policy_versions");
        }
    }
}
