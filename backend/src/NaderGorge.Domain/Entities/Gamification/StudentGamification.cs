using System;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Gamification;

public class StudentGamification
{
    public Guid StudentId { get; set; }  // Primary Key

    // Kept here for easy queries, real-time leaderboard is in Redis
    public int TotalPoints { get; set; }

    public int CurrentStreakCount { get; set; }
    public int LongestStreakCount { get; set; }

    public DateTime? LastTaskCompletedAt { get; set; }
    public string LevelName { get; set; } = "Novice";

    public User Student { get; set; } = null!;
}
