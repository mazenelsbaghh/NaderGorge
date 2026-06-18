using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class VideoPlaybackSession : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;

    public string SessionToken { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public bool IsConsumed { get; set; }
    public string? IpAddress { get; set; }

    // Progress lifecycle is separate from one-time embed material consumption.
    public bool HasRegisteredView { get; set; }
    public long LastProgressSequence { get; set; }
    public DateTime? LastProgressAt { get; set; }
    public bool IsSuperseded { get; set; }
}
