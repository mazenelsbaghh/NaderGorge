using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAIConversationState
{
    public Guid ConversationId { get; set; }
    public LiveSupportAIMode Mode { get; set; }
    public Guid PolicyVersionId { get; set; }
    public Guid? VerifiedStudentUserId { get; set; }
    public DateTime LastParticipantActivityAt { get; set; }
    public DateTime? InactivityWarningSentAt { get; set; }
    public DateTime? AutoCloseAt { get; set; }
    public string? HandoffReasonCode { get; set; }
    public string? HandoffSafeSummary { get; set; }
    public DateTime? HandedOffAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionCode { get; set; }
    public string? SafeSummaryJson { get; set; }
    public long LastEventSequence { get; set; }
    public DateTime? DisableRequestedAt { get; set; }
    public DateTime? LastRecoveryAt { get; set; }
    public long Version { get; set; }
}
