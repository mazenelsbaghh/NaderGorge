using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportContactBlock : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid? StudentUserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public string? PhoneNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid BlockedByUserId { get; set; }
    public DateTime? UnblockedAt { get; set; }
    public Guid? UnblockedByUserId { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportBlockDelivery : BaseEntity
{
    public Guid BlockId { get; set; }
    public Guid? AccountId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public bool DesiredBlocked { get; set; } = true;
    public string Status { get; set; } = "Pending";
    public string NoticeStatus { get; set; } = "Pending";
    public string? FailureCode { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public long Version { get; set; }
}
