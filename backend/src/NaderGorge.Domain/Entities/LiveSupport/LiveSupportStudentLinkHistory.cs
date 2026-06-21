using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportStudentLinkHistory : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid? PreviousStudentUserId { get; set; }
    public Guid? NewStudentUserId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
