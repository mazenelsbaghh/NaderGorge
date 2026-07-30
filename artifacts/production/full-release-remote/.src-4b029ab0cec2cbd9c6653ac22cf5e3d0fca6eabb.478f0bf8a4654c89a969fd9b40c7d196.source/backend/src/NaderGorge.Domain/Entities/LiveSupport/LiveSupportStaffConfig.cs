using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportStaffConfig : BaseEntity
{
    public Guid UserId { get; set; }
    public bool IsEnabled { get; set; }
    public int MaxActiveConversations { get; set; } = 1;
    public DateTime? LastAssignedAt { get; set; }
    public Guid ConfiguredByUserId { get; set; }
    public long Version { get; set; }
}
