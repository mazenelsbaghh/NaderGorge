using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.Notifications;

public class ParentDeviceToken : BaseEntity
{
    public Guid StudentId { get; set; }
    public StudentProfile Student { get; set; } = null!;
    
    public string DeviceToken { get; set; } = string.Empty; // FCM (Android) or APNs (iOS) token
    public string Platform { get; set; } = string.Empty;    // "android" or "ios"
}
