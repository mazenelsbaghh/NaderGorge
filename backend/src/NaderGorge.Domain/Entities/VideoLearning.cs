using System.ComponentModel.DataAnnotations;
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class VideoLearningConfiguration : BaseEntity
{
    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;
    public int SourceRevision { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public string DocumentJson { get; set; } = "{}";
}

public sealed class VideoLearningEntry : BaseEntity
{
    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public int SourceRevision { get; set; }
    public Guid ConfigurationVersion { get; set; }
    public Guid? ActivityId { get; set; }
    [MaxLength(24)] public string Kind { get; set; } = "note";
    public int Seconds { get; set; }
    [MaxLength(2000)] public string Text { get; set; } = "";
    [MaxLength(300)] public string Title { get; set; } = "";
    public bool? Correct { get; set; }
    public string? ResultJson { get; set; }
    public Guid? CommentId { get; set; }
}
