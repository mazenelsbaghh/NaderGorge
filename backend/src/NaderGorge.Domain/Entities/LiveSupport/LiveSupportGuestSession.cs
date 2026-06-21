using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportGuestSession : BaseEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string SecurityStampHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string CreatedIpHash { get; set; } = string.Empty;
    public string? UserAgentSummary { get; set; }
}
