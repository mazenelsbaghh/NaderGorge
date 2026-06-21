using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportQueueEntry : BaseEntity
{
    public Guid ConversationId { get; set; }
    public DateTime EnteredAt { get; set; }
    public long Sequence { get; set; }
    public DateTime? DequeuedAt { get; set; }
    public string? DequeueReason { get; set; }
}
