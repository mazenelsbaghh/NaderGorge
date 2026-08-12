using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.AdminAI;

public sealed class AdminAIAuditEvent : BaseEntity
{
    public AdminAIAuditEventType EventType { get; set; }
    public Guid? ActorAdminUserId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? TurnId { get; set; }
    public Guid? ReadInvocationId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid? ExecutionId { get; set; }
    public string? CapabilityKey { get; set; }
    public string? SafeTargetReference { get; set; }
    public string SafeEvidenceJson { get; set; } = "{}";
    public string EvidenceHash { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string? IpAddressHash { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
