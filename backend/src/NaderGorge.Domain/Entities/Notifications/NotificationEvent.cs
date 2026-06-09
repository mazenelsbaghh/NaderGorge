using System;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Notifications;

public enum NotificationChannelType
{
    InApp,
    SMS
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}

public class NotificationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Can be Student, Parent, or Assistant
    public Guid UserId { get; set; }

    public NotificationChannelType ChannelType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
