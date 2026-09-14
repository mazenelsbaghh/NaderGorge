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

    // A playback session must keep the tracking contract that was presented to
    // the student. Provider metadata or platform settings can change while the
    // same iframe is open, so progress calls must not recalculate these values.
    public int? TrackingDurationSeconds { get; set; }
    public int? TrackingThresholdPercentage { get; set; }
    public int? TrackingThresholdSeconds { get; set; }

    // Playback-rate weighting can produce fractions (for example one wall-clock
    // second at 0.5x). Keep the fraction on the session instead of rounding every
    // request up and over-crediting repeated short chunks.
    public decimal SpeedAdjustedSecondsRemainder { get; set; }

    // Cumulative server-accepted wall time anchors batch anti-speed checks to the
    // session lifetime, so a duplicate request cannot reset the available budget.
    public decimal AcceptedWallSeconds { get; set; }
}
