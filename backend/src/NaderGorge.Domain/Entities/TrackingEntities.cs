using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class VideoWatchEvent : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;

    // Cumulative time watched in seconds
    public int TimeWatchedInSeconds { get; set; }
    
    public int WatchCount { get; set; }
    public bool IsLocked { get; set; }
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
