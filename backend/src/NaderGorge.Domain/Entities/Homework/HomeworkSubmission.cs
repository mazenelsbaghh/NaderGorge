using System;
using System.Collections.Generic;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Homework;

public enum SubmissionStatus
{
    InProgress,
    PendingReview,
    Graded,
    Missed
}

public class HomeworkSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HomeworkId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.InProgress;
    
    // The Assistant who reviewed this, nullable.
    public Guid? AssistantReviewerId { get; set; }
    public string? AssistantNotes { get; set; }
    public decimal OverallScore { get; set; }
    
    // Auto-calculated evaluation string (e.g. "ممتاز")
    public string? Evaluation { get; set; }

    public Homework Homework { get; set; } = null!;
    public User Student { get; set; } = null!;
    public User? AssistantReviewer { get; set; }

    public ICollection<HomeworkAnswer> Answers { get; set; } = new List<HomeworkAnswer>();
}
