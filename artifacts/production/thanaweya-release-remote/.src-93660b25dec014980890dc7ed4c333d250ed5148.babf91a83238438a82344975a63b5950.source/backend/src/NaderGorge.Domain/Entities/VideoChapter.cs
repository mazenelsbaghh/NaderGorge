using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class VideoChapter : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int StartTime { get; set; }
    public int EndTime { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string? MindmapImageUrl { get; set; }
    public int Order { get; set; }

    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;
}
