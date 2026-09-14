using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;

namespace NaderGorge.Infrastructure.Data;

internal static class AdminAIEntityConfigurations
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureGovernance(modelBuilder);
        ConfigureConversation(modelBuilder);
        ConfigureTurn(modelBuilder);
        ConfigureProposal(modelBuilder);
        ConfigureExecution(modelBuilder);
        ConfigureAudit(modelBuilder);
    }

    private static void ConfigureGovernance(ModelBuilder b)
    {
        b.Entity<AdminAICapabilityBaseline>(e =>
        {
            e.ToTable("admin_ai_capability_baselines", table => table.HasCheckConstraint("ck_admin_ai_baseline_counts", "\"SupportedReadCount\" >= 0 AND \"SupportedActionCount\" >= 0 AND \"ExcludedCount\" >= 0"));
            e.HasIndex(x => x.Version).IsUnique(); e.HasIndex(x => x.ManifestHash).IsUnique();
            e.HasIndex(x => x.Status).IsUnique().HasFilter("\"Status\" = 1");
            e.Property(x => x.Version).HasMaxLength(64); e.Property(x => x.ManifestHash).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.SafeManifestJson).HasColumnType("jsonb"); e.Property(x => x.SourceRevision).HasMaxLength(100);
            e.Property(x => x.RuntimeInventoryHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.FrontendInventoryHash).HasMaxLength(64).IsFixedLength();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AdminAISensitiveDataPolicyVersion>(e =>
        {
            e.ToTable("admin_ai_sensitive_policy_versions");
            e.HasIndex(x => x.Version).IsUnique(); e.HasIndex(x => x.PolicyHash).IsUnique(); e.HasIndex(x => x.Status).IsUnique().HasFilter("\"Status\" = 1");
            e.Property(x => x.Version).HasMaxLength(64); e.Property(x => x.PolicyHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.SafeRulesJson).HasColumnType("jsonb");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureConversation(ModelBuilder b)
    {
        b.Entity<AdminAIConversation>(e =>
        {
            e.ToTable("admin_ai_conversations", table => table.HasCheckConstraint("ck_admin_ai_conversation_version", "\"LastSequence\" >= 0 AND \"Version\" > 0"));
            e.Property(x => x.Title).HasMaxLength(160); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.CreateIdempotencyDigest).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.CreatePayloadHash).HasMaxLength(64).IsFixedLength();
            e.HasIndex(x => new { x.OwnerAdminUserId, x.CreateIdempotencyDigest }).IsUnique().HasFilter("\"CreateIdempotencyDigest\" IS NOT NULL");
            e.HasIndex(x => new { x.OwnerAdminUserId, x.Status, x.LastActivityAt, x.Id });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerAdminUserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AdminAIConversationCommandReceipt>(e =>
        {
            e.ToTable("admin_ai_conversation_command_receipts", table => table.HasCheckConstraint("ck_admin_ai_conversation_receipt_version", "\"ResponseVersion\" > 0"));
            e.Property(x => x.Operation).HasMaxLength(32);
            e.Property(x => x.IdempotencyDigest).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.ResponseTitle).HasMaxLength(160);
            e.HasIndex(x => new { x.OwnerAdminUserId, x.IdempotencyDigest }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerAdminUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAIConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AdminAIMessage>(e =>
        {
            e.ToTable("admin_ai_messages"); e.Property(x => x.Content).HasMaxLength(16000); e.Property(x => x.StructuredContentJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique(); e.HasIndex(x => x.TurnId).IsUnique().HasFilter("\"TurnId\" IS NOT NULL");
            e.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAITurn>().WithOne().HasForeignKey<AdminAIMessage>(x => x.TurnId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTurn(ModelBuilder b)
    {
        b.Entity<AdminAITurn>(e =>
        {
            e.ToTable("admin_ai_turns", table => table.HasCheckConstraint("ck_admin_ai_turn_budgets", "\"CurrentStepNumber\" BETWEEN 0 AND 3 AND \"ReadInvocationCount\" BETWEEN 0 AND 6 AND \"RedactedContextBytes\" BETWEEN 0 AND 65536 AND \"Version\" > 0"));
            e.Property(x => x.CallbackIdempotencyDigest).HasMaxLength(64).IsFixedLength(); e.HasIndex(x => x.CallbackIdempotencyDigest).IsUnique(); e.HasIndex(x => x.SourceMessageId).IsUnique();
            e.Property(x => x.AdmissionPayloadHash).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.Provider).HasMaxLength(64); e.Property(x => x.Model).HasMaxLength(128); e.Property(x => x.ProviderResponseId).HasMaxLength(256); e.Property(x => x.FailureCode).HasMaxLength(100); e.Property(x => x.SafeFailureDetail).HasMaxLength(500); e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.Status, x.QueuedAt }); e.HasIndex(x => new { x.ActorAdminUserId, x.Status }); e.HasIndex(x => new { x.ConversationId, x.QueuedAt });
            e.HasOne(x => x.Conversation).WithMany(x => x.Turns).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorAdminUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAICapabilityBaseline>().WithMany().HasForeignKey(x => x.CapabilityBaselineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAISensitiveDataPolicyVersion>().WithMany().HasForeignKey(x => x.SensitiveDataPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AdminAITurnStep>(e =>
        {
            e.ToTable("admin_ai_turn_steps", table => table.HasCheckConstraint("ck_admin_ai_step_bounds", "\"StepNumber\" BETWEEN 1 AND 3 AND \"ToolCallsRequested\" BETWEEN 0 AND 4"));
            e.HasIndex(x => new { x.TurnId, x.StepNumber }).IsUnique(); e.Property(x => x.CanonicalDecisionHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.CallbackStatus).HasMaxLength(32); e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne(x => x.Turn).WithMany(x => x.Steps).HasForeignKey(x => x.TurnId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AdminAIReadInvocation>(e =>
        {
            e.ToTable("admin_ai_read_invocations", table => table.HasCheckConstraint("ck_admin_ai_read_bounds", "\"InvocationSequence\" BETWEEN 1 AND 6 AND \"ResultCount\" >= 0 AND \"LatencyMs\" >= 0"));
            e.HasIndex(x => new { x.TurnId, x.InvocationSequence }).IsUnique(); e.HasIndex(x => new { x.CapabilityKey, x.CreatedAt });
            e.Property(x => x.CapabilityKey).HasMaxLength(160); e.Property(x => x.CapabilityVersion).HasMaxLength(64); e.Property(x => x.SafeInputJson).HasColumnType("jsonb"); e.Property(x => x.SafeScopeJson).HasColumnType("jsonb"); e.Property(x => x.SafeEvidenceJson).HasColumnType("jsonb"); e.Property(x => x.InputHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.ProtectedResultHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.TraceId).HasMaxLength(64);
            e.HasOne(x => x.Turn).WithMany(x => x.ReadInvocations).HasForeignKey(x => x.TurnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAITurnStep>().WithMany().HasForeignKey(x => x.TurnStepId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProposal(ModelBuilder b)
    {
        b.Entity<AdminAIActionProposal>(e =>
        {
            e.ToTable("admin_ai_action_proposals"); e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.CapabilityKey).HasMaxLength(160); e.Property(x => x.CapabilityVersion).HasMaxLength(64); e.Property(x => x.SafeTargetType).HasMaxLength(100); e.Property(x => x.SafeTargetReference).HasMaxLength(200);
            foreach (var p in new[] { nameof(AdminAIActionProposal.RiskFlagsJson), nameof(AdminAIActionProposal.SafeCurrentStateJson), nameof(AdminAIActionProposal.SafeRequestedStateJson), nameof(AdminAIActionProposal.SafeEffectJson), nameof(AdminAIActionProposal.ValidationSummaryJson), nameof(AdminAIActionProposal.BulkSemanticsJson) }) e.Property(p).HasColumnType("jsonb");
            e.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.StateFingerprint).HasMaxLength(64).IsFixedLength(); e.HasIndex(x => new { x.ActorAdminUserId, x.Status, x.ExpiresAt }); e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            e.HasOne<AdminAIConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAITurn>().WithMany().HasForeignKey(x => x.TurnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorAdminUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAICapabilityBaseline>().WithMany().HasForeignKey(x => x.CapabilityBaselineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAISensitiveDataPolicyVersion>().WithMany().HasForeignKey(x => x.SensitiveDataPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AdminAIConfirmationChallenge>(e => { e.ToTable("admin_ai_confirmation_challenges"); e.HasIndex(x => x.ProposalId).IsUnique(); e.Property(x => x.PhraseDigest).HasMaxLength(64).IsFixedLength(); e.Property(x => x.ChallengeVersion).HasMaxLength(16); e.Property(x => x.Version).IsConcurrencyToken(); e.HasOne<AdminAIActionProposal>().WithOne().HasForeignKey<AdminAIConfirmationChallenge>(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<AdminAISecureInputGrant>(e => { e.ToTable("admin_ai_secure_input_grants"); e.HasIndex(x => x.ProposalId).IsUnique(); e.HasIndex(x => x.TokenDigest).IsUnique(); e.Property(x => x.InputKind).HasMaxLength(64); e.Property(x => x.TokenDigest).HasMaxLength(64).IsFixedLength(); e.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.SafeMetadataJson).HasColumnType("jsonb"); e.Property(x => x.Version).IsConcurrencyToken(); e.HasOne<AdminAIActionProposal>().WithOne().HasForeignKey<AdminAISecureInputGrant>(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict); e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorAdminUserId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureExecution(ModelBuilder b)
    {
        b.Entity<AdminAIActionExecution>(e => { e.ToTable("admin_ai_action_executions"); e.HasIndex(x => x.ProposalId).IsUnique(); e.HasIndex(x => new { x.ActorAdminUserId, x.IdempotencyDigest }).IsUnique(); e.Property(x => x.IdempotencyDigest).HasMaxLength(64).IsFixedLength(); e.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.AuthoritativeOperation).HasMaxLength(200); e.Property(x => x.SafeResultJson).HasColumnType("jsonb"); e.Property(x => x.RefreshScopesJson).HasColumnType("jsonb"); e.Property(x => x.TraceId).HasMaxLength(64); e.Property(x => x.Version).IsConcurrencyToken(); e.HasOne<AdminAIActionProposal>().WithOne().HasForeignKey<AdminAIActionExecution>(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict); e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorAdminUserId).OnDelete(DeleteBehavior.Restrict); e.HasOne<AuditLog>().WithMany().HasForeignKey(x => x.OriginalAuditLogId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<AdminAIActionExecutionItem>(e => { e.ToTable("admin_ai_action_execution_items"); e.HasIndex(x => new { x.ExecutionId, x.ItemSequence }).IsUnique(); e.HasIndex(x => new { x.ExecutionId, x.ItemReferenceHash }).IsUnique(); e.Property(x => x.SafeItemReference).HasMaxLength(200); e.Property(x => x.ItemReferenceHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.SafeResultJson).HasColumnType("jsonb"); e.HasOne(x => x.Execution).WithMany(x => x.Items).HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureAudit(ModelBuilder b)
    {
        b.Entity<AdminAIAuditEvent>(e => { e.ToTable("admin_ai_audit_events"); e.Property(x => x.CapabilityKey).HasMaxLength(160); e.Property(x => x.SafeTargetReference).HasMaxLength(200); e.Property(x => x.SafeEvidenceJson).HasColumnType("jsonb"); e.Property(x => x.EvidenceHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.CorrelationId).HasMaxLength(100); e.Property(x => x.TraceId).HasMaxLength(64); e.Property(x => x.RequestId).HasMaxLength(100); e.Property(x => x.IpAddressHash).HasMaxLength(64).IsFixedLength(); e.HasIndex(x => new { x.ConversationId, x.OccurredAt, x.Id }); e.HasIndex(x => new { x.ProposalId, x.OccurredAt }); e.HasIndex(x => new { x.ExecutionId, x.OccurredAt }); e.HasIndex(x => new { x.ActorAdminUserId, x.OccurredAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorAdminUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAIConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAITurn>().WithMany().HasForeignKey(x => x.TurnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAIReadInvocation>().WithMany().HasForeignKey(x => x.ReadInvocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAIActionProposal>().WithMany().HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AdminAIActionExecution>().WithMany().HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
