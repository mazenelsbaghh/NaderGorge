using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class VideoWatchEvent : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;

    // Cumulative, speed-adjusted time used to calculate watch progress.
    public int TimeWatchedInSeconds { get; set; }

    // Real elapsed playback time. This keeps reporting independent from the
    // speed-adjusted progress value above.
    public decimal ActualWatchedSeconds { get; set; }

    public decimal LastPlaybackRate { get; set; } = 1m;

    // Actual wall-clock seconds grouped by playback rate, e.g. {"1":42,"2":61}.
    public string PlaybackRateBreakdownJson { get; set; } = "{}";

    public int WatchCount { get; set; }
    public bool IsLocked { get; set; }
    public int? CustomMaxWatchCount { get; set; }
}

public class LessonProgress : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public bool IsCompleted { get; set; }

    // Support Teacher/Assistant-Controlled Gating
    public bool IsManuallyUnlocked { get; set; }
}

public class VideoOverride : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;

    public int OriginalLimit { get; set; }
    public int NewLimit { get; set; }
    public int AddedViews { get; set; }
    public string Reason { get; set; } = string.Empty;

    public Guid PerformedByUserId { get; set; }
    public User PerformedByUser { get; set; } = null!;
}
