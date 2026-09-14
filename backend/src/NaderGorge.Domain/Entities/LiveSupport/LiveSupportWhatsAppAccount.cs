using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportWhatsAppAccount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
    public string Status { get; set; } = "Created";
    public string? PhoneNumber { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public long Version { get; set; }
}
