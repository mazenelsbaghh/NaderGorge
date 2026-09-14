using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.Notifications;

public enum AssessmentParentDeliveryStatus { Pending, Sending, Sent, Skipped, Failed, Uncertain }

public sealed class AssessmentParentDelivery : BaseEntity
{
    public string AssessmentKind { get; set; } = string.Empty;
    public Guid AssessmentId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid StudentUserId { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateFingerprint { get; set; } = string.Empty;
    public string DestinationHash { get; set; } = string.Empty;
    public byte[] ProtectedPayload { get; set; } = [];
    public string PayloadDigest { get; set; } = string.Empty;
    public AssessmentParentDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? MetaMessageId { get; set; }
    public string? FailureCode { get; set; }
}
