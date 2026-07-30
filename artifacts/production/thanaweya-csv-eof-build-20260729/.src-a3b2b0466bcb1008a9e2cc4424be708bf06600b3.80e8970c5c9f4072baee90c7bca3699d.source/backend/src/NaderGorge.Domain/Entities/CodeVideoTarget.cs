using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

/// <summary>
/// Join table for Video code types — maps a CodeGroup to specific LessonVideos
/// </summary>
public class CodeVideoTarget : BaseEntity
{
    public Guid CodeGroupId { get; set; }
    public CodeGroup CodeGroup { get; set; } = null!;

    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;
}
