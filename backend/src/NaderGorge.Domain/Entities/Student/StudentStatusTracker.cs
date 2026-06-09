using System;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Student;

public enum StudentCommitmentStatus
{
    Committed,
    Average,
    AtRisk
}

public class StudentStatusTracker
{
    public Guid StudentId { get; set; }
    public StudentCommitmentStatus CurrentStatus { get; set; } = StudentCommitmentStatus.Committed;

    public int ConsecutiveMissedHomeworks { get; set; }
    public int ConsecutiveFailedExams { get; set; }

    public DateTime? LastActiveAt { get; set; }
    public DateTime LastEvaluatedAt { get; set; } = DateTime.UtcNow;

    public User Student { get; set; } = null!;
}
