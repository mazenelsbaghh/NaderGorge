using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public enum WhatsAppCampaignStatus
{
    Draft = 0,
    Locked = 1,
    Running = 2,
    Paused = 3,
    Completed = 4,
    Cancelled = 5,
    Failed = 6
}

public enum WhatsAppCampaignRecipientStatus
{
    Pending = 0,
    Sending = 1,
    Sent = 2,
    Delivered = 3,
    Read = 4,
    Failed = 5,
    Skipped = 6,
    Uncertain = 7
}

public enum WhatsAppContactPreferenceCategory
{
    Utility = 0,
    Marketing = 1,
    All = 2
}

public enum WhatsAppContactPreferenceState
{
    OptedIn = 0,
    OptedOut = 1
}

public enum WhatsAppTemplateSyncRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2
}

public sealed class WhatsAppCampaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string TemplateMetaId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateLanguage { get; set; } = string.Empty;
    public string TemplateCategory { get; set; } = string.Empty;
    public string TemplateComponentsJson { get; set; } = "[]";
    public string TemplateFingerprint { get; set; } = string.Empty;
    public string AudienceFilterJson { get; set; } = "{}";
    public string VariableMappingsJson { get; set; } = "[]";
    public string AudienceFingerprint { get; set; } = string.Empty;
    public WhatsAppCampaignStatus Status { get; set; } = WhatsAppCampaignStatus.Draft;
    public int RecipientCount { get; set; }
    public int ExcludedCount { get; set; }
    public string ExclusionSummaryJson { get; set; } = "{}";
    public int PendingCount { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int UncertainCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? LastChangedByUserId { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? LaunchedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? PauseReason { get; set; }
    public string CreateIdempotencyKey { get; set; } = string.Empty;
    public string CreateRequestHash { get; set; } = string.Empty;
    public string ReviewTokenHash { get; set; } = string.Empty;
    public byte[] ProtectedReviewToken { get; set; } = [];
    public string ProtectedReviewTokenDigest { get; set; } = string.Empty;
    public DateTime ReviewTokenExpiresAt { get; set; }
    public string ConfirmationPhraseHash { get; set; } = string.Empty;
    public string? LaunchIdempotencyKey { get; set; }
    public string? LaunchRequestHash { get; set; }
    public long Version { get; set; }
}

public sealed class WhatsAppCampaignRecipient : BaseEntity
{
    public Guid CampaignId { get; set; }
    public Guid StudentUserId { get; set; }
    public string ContactRole { get; set; } = "StudentPrimary";
    public string DestinationHash { get; set; } = string.Empty;
    public string DestinationLast4 { get; set; } = string.Empty;
    public byte[] ProtectedPayload { get; set; } = [];
    public string PayloadDigest { get; set; } = string.Empty;
    public WhatsAppCampaignRecipientStatus Status { get; set; } = WhatsAppCampaignRecipientStatus.Pending;
    public string? MetaMessageId { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ProviderTimestamp { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? FailureCode { get; set; }
    public long Version { get; set; }
}

/// <summary>
/// Append-only consent evidence. The latest effective record for a destination hash and
/// category is authoritative. Consent is never inferred from purchases or inbound support.
/// </summary>
public sealed class WhatsAppContactPreference : BaseEntity
{
    public Guid? StudentUserId { get; set; }
    public string ContactRole { get; set; } = "StudentPrimary";
    public string DestinationHash { get; set; } = string.Empty;
    public string DestinationLast4 { get; set; } = string.Empty;
    public WhatsAppContactPreferenceCategory Category { get; set; }
    public WhatsAppContactPreferenceState State { get; set; }
    public string Source { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime EffectiveAt { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public Guid? SupersedesPreferenceId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string? SourceMessageId { get; set; }
}

public sealed class WhatsAppCampaignAuditEvent : BaseEntity
{
    public Guid? CampaignId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SafeMetadataJson { get; set; } = "{}";
}

public sealed class WhatsAppTemplateSyncRun : BaseEntity
{
    public Guid? RequestedByUserId { get; set; }
    public WhatsAppTemplateSyncRunStatus Status { get; set; } = WhatsAppTemplateSyncRunStatus.Running;
    public int ReceivedCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int StaleCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureCode { get; set; }
}
