using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportConversation : BaseEntity
{
    public LiveSupportParticipantType ParticipantType { get; set; }
    public Guid? StudentUserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public Guid? LinkedStudentUserId { get; set; }
    public Guid? PreviousConversationId { get; set; }
    public LiveSupportConversationStatus Status { get; set; }
    public Guid? CurrentOwnerUserId { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? FirstStaffResponseAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? CloseReason { get; set; }
    public string? Subject { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public long Version { get; set; }
}
