using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

/// <summary>
/// Lesson-owned MIM game state. Draft and published JSON are deliberately
/// separate: AI generation can never replace a student-visible game.
/// </summary>
public class LessonMimGame : BaseEntity
{
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public string? DraftContentJson { get; set; }
    public string? DraftFingerprint { get; set; }
    public string? PublishedContentJson { get; set; }
    public string? PublishedFingerprint { get; set; }

    public LessonMimGameStatus Status { get; set; } = LessonMimGameStatus.Draft;
    public bool IsEnabled { get; set; }
    public Guid? CurrentGenerationRunId { get; set; }
    public string? LastError { get; set; }
    public DateTime? GenerationStartedAtUtc { get; set; }
    public DateTime? GenerationExpiresAtUtc { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public long Version { get; set; }
}

public enum LessonMimGameStatus
{
    Draft = 0,
    Generating = 1,
    Ready = 2,
    Failed = 3,
    Stale = 4
}
