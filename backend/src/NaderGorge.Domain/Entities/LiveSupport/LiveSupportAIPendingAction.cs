using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAIPendingAction : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid TurnId { get; set; }
    public LiveSupportAIPendingDecisionKind DecisionKind { get; set; }
    public Guid? StudentUserId { get; set; }
    public Guid PolicyVersionId { get; set; }
    public string ActionKey { get; set; } = string.Empty;
    public string SafeProposalJson { get; set; } = "{}";
    public byte[]? EncryptedPayload { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string StateFingerprint { get; set; } = string.Empty;
    public string ConfirmationNonceHash { get; set; } = string.Empty;
    public string? CallbackDecisionHash { get; set; }
    public Guid IdempotencyKey { get; set; }
    public LiveSupportAIPendingActionStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public Guid? ConfirmedByGuestSessionId { get; set; }
    public Guid? ActionExecutionId { get; set; }
    public string? FailureCode { get; set; }
    public long Version { get; set; }
}
