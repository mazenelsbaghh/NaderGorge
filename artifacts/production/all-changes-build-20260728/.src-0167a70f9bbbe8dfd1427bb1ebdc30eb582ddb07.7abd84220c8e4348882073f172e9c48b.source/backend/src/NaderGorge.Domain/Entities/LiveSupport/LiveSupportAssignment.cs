using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportAssignment : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid StaffUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public LiveSupportAssignmentEndReason? EndReason { get; set; }
    public Guid? AssignedByUserId { get; set; }
    public string? TransferReason { get; set; }
    public int AssignmentSequence { get; set; }
}
