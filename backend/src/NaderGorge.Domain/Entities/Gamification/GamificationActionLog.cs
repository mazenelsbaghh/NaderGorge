using System;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Gamification;

public enum GamificationEventType
{
    HomeworkSubmittedOnTime,
    PerfectExam,
    StreakMaintained,
    EarlyBird
}

public class GamificationActionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }

    public GamificationEventType EventType { get; set; }
    public int PointsAwarded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Student { get; set; } = null!;
}
