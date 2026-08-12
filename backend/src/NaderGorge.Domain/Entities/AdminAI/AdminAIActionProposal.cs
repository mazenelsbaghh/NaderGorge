using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.AdminAI;

public sealed class AdminAIActionProposal : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid TurnId { get; set; }
    public Guid ActorAdminUserId { get; set; }
    public Guid CapabilityBaselineId { get; set; }
    public Guid SensitiveDataPolicyVersionId { get; set; }
    public string CapabilityKey { get; set; } = string.Empty;
    public string CapabilityVersion { get; set; } = string.Empty;
    public AdminAIRiskCategory PrimaryRisk { get; set; }
    public string RiskFlagsJson { get; set; } = "[]";
    public AdminAIConfirmationType ConfirmationType { get; set; }
    public string SafeTargetType { get; set; } = string.Empty;
    public string SafeTargetReference { get; set; } = string.Empty;
    public byte[] ProtectedNormalizedPayload { get; set; } = [];
    public string PayloadHash { get; set; } = string.Empty;
    public string StateFingerprint { get; set; } = string.Empty;
    public string SafeCurrentStateJson { get; set; } = "{}";
    public string SafeRequestedStateJson { get; set; } = "{}";
    public string SafeEffectJson { get; set; } = "{}";
    public string ValidationSummaryJson { get; set; } = "{}";
    public string? BulkSemanticsJson { get; set; }
    public Guid? SecureInputGrantId { get; set; }
    public AdminAIProposalStatus Status { get; set; } = AdminAIProposalStatus.PendingConfirmation;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? InvalidatedReasonCode { get; set; }
    public string? FailureCode { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class AdminAIConfirmationChallenge : BaseEntity
{
    public Guid ProposalId { get; set; }
    public string PhraseDigest { get; set; } = string.Empty;
    public string ChallengeVersion { get; set; } = "v1";
    public AdminAIChallengeStatus Status { get; set; } = AdminAIChallengeStatus.Pending;
    public int FailedAttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class AdminAISecureInputGrant : BaseEntity
{
    public Guid ProposalId { get; set; }
    public Guid ActorAdminUserId { get; set; }
    public string InputKind { get; set; } = string.Empty;
    public string TokenDigest { get; set; } = string.Empty;
    public byte[]? ProtectedPayload { get; set; }
    public string? PayloadHash { get; set; }
    public string SafeMetadataJson { get; set; } = "{}";
    public AdminAISecureInputGrantStatus Status { get; set; } = AdminAISecureInputGrantStatus.Issued;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PurgedAt { get; set; }
    public long Version { get; set; } = 1;
}
