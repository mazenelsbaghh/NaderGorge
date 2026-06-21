using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportEvent : BaseEntity
{
    public Guid ConversationId { get; set; }
    public LiveSupportEventType Type { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? ActorGuestSessionId { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? SafeMetadataJson { get; set; }
    public DateTime OccurredAt { get; set; }
    public long Sequence { get; set; }
}
