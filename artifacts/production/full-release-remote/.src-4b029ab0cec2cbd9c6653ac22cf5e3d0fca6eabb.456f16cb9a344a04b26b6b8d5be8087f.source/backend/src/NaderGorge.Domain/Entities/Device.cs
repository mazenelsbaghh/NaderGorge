using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class Device : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceName { get; set; }      // Raw User-Agent string
    public string? IpAddress { get; set; }
    public string? OsName { get; set; }           // e.g. "Windows", "Android", "iOS"
    public string? BrowserName { get; set; }      // e.g. "Chrome", "Safari", "Firefox"
    public string? DeviceType { get; set; }       // "Mobile", "Tablet", "Desktop"
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
