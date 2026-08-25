using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCampaignsAndPackagePurchaseControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentMindmapGenerationRunId",
                table: "video_chapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiOutputLanguage",
                table: "packages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<bool>(
                name: "AllowFullPackagePurchase",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "live_support_whatsapp_templates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Existing templates stay fail-closed until the reviewed Meta sync computes
            // the canonical SHA-256 fingerprint. New rows must always provide one.
            migrationBuilder.Sql(
                "ALTER TABLE live_support_whatsapp_templates " +
                "ALTER COLUMN \"Fingerprint\" DROP DEFAULT;");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAiAnalysisRunId",
                table: "lesson_videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentMindmapGenerationRunId",
                table: "lesson_videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "whatsapp_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateMetaId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TemplateLanguage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TemplateCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TemplateComponentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TemplateFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AudienceFilterJson = table.Column<string>(type: "jsonb", nullable: false),
                    VariableMappingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AudienceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    ExclusionSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    PendingCount = table.Column<int>(type: "integer", nullable: false),
                    SentCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredCount = table.Column<int>(type: "integer", nullable: false),
                    ReadCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    UncertainCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LaunchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PausedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PauseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreateIdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreateRequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedReviewToken = table.Column<byte[]>(type: "bytea", nullable: false),
                    ProtectedReviewTokenDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewTokenExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ConfirmationPhraseHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LaunchIdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LaunchRequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_campaigns_live_support_whatsapp_templates_Template~",
                        column: x => x.TemplateId,
                        principalTable: "live_support_whatsapp_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_campaigns_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_campaigns_users_LastChangedByUserId",
                        column: x => x.LastChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_contact_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactRole = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    DestinationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DestinationLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesPreferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_contact_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_contact_preferences_users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_contact_preferences_users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_contact_preferences_whatsapp_contact_preferences_S~",
                        column: x => x.SupersedesPreferenceId,
                        principalTable: "whatsapp_contact_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_template_sync_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceivedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    StaleCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_template_sync_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_template_sync_runs_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_campaign_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SafeMetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_campaign_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_campaign_audit_events_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_campaign_audit_events_whatsapp_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "whatsapp_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_campaign_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactRole = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    DestinationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DestinationLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ProtectedPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MetaMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProviderTimestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_campaign_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_campaign_recipients_users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_campaign_recipients_whatsapp_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "whatsapp_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaign_audit_events_ActorUserId",
                table: "whatsapp_campaign_audit_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaign_audit_events_CampaignId_CreatedAt",
                table: "whatsapp_campaign_audit_events",
                columns: new[] { "CampaignId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaign_recipients_CampaignId_DestinationHash",
                table: "whatsapp_campaign_recipients",
                columns: new[] { "CampaignId", "DestinationHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaign_recipients_CampaignId_Status",
                table: "whatsapp_campaign_recipients",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaign_recipients_MetaMessageId",
                table: "whatsapp_campaign_recipients",
                column: "MetaMessageId",
                unique: true,
                filter: "\"MetaMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaign_recipients_Status_NextAttemptAt_CreatedAt",
                table: "whatsapp_campaign_recipients",
                columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaign_recipients_StudentUserId",
                table: "whatsapp_campaign_recipients",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaigns_CreatedByUserId_CreateIdempotencyKey",
                table: "whatsapp_campaigns",
                columns: new[] { "CreatedByUserId", "CreateIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaigns_CreatedByUserId_LaunchIdempotencyKey",
                table: "whatsapp_campaigns",
                columns: new[] { "CreatedByUserId", "LaunchIdempotencyKey" },
                unique: true,
                filter: "\"LaunchIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaigns_LastChangedByUserId",
                table: "whatsapp_campaigns",
                column: "LastChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaigns_Status_CreatedAt",
                table: "whatsapp_campaigns",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_campaigns_TemplateId_Status",
                table: "whatsapp_campaigns",
                columns: new[] { "TemplateId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_contact_preferences_DestinationHash_Category_Effec~",
                table: "whatsapp_contact_preferences",
                columns: new[] { "DestinationHash", "Category", "EffectiveAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_contact_preferences_RecordedByUserId_IdempotencyKey",
                table: "whatsapp_contact_preferences",
                columns: new[] { "RecordedByUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_contact_preferences_SourceMessageId",
                table: "whatsapp_contact_preferences",
                column: "SourceMessageId",
                unique: true,
                filter: "\"SourceMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_contact_preferences_StudentUserId",
                table: "whatsapp_contact_preferences",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_contact_preferences_SupersedesPreferenceId",
                table: "whatsapp_contact_preferences",
                column: "SupersedesPreferenceId",
                unique: true,
                filter: "\"SupersedesPreferenceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_sync_runs_RequestedByUserId",
                table: "whatsapp_template_sync_runs",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_sync_runs_Status",
                table: "whatsapp_template_sync_runs",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_sync_runs_Status_StartedAt",
                table: "whatsapp_template_sync_runs",
                columns: new[] { "Status", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_campaign_audit_events");

            migrationBuilder.DropTable(
                name: "whatsapp_campaign_recipients");

            migrationBuilder.DropTable(
                name: "whatsapp_contact_preferences");

            migrationBuilder.DropTable(
                name: "whatsapp_template_sync_runs");

            migrationBuilder.DropTable(
                name: "whatsapp_campaigns");

            migrationBuilder.DropColumn(
                name: "CurrentMindmapGenerationRunId",
                table: "video_chapters");

            migrationBuilder.DropColumn(
                name: "AiOutputLanguage",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "AllowFullPackagePurchase",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "live_support_whatsapp_templates");

            migrationBuilder.DropColumn(
                name: "CurrentAiAnalysisRunId",
                table: "lesson_videos");

            migrationBuilder.DropColumn(
                name: "CurrentMindmapGenerationRunId",
                table: "lesson_videos");
        }
    }
}
