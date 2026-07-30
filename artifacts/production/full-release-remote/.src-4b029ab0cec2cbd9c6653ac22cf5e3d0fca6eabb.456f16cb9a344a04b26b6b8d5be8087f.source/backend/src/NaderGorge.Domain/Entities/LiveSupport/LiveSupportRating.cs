using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportRating : BaseEntity
{
    public Guid ConversationId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public Guid? SubmittedByGuestSessionId { get; set; }
    public DateTime SubmittedAt { get; set; }
}
