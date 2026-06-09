using System;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Assistant;

public enum AssistantTaskType
{
    GradeEssay,
    FollowUpAtRisk,
    ResolvePaymentIssue
}

public enum AssistantTaskStatus
{
    Open,
    InReview,
    Done
}

public class AssistantTaskQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public AssistantTaskType TaskType { get; set; }
    public Guid ReferenceEntityId { get; set; }

    public Guid StudentId { get; set; }
    public Guid? AssignedAssistantId { get; set; }

    public AssistantTaskStatus Status { get; set; } = AssistantTaskStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User Student { get; set; } = null!;
    public User? AssignedAssistant { get; set; }
}
