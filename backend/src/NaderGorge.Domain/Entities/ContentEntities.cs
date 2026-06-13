using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

/// <summary>
/// Package represents the academic year.
/// Contains Terms directly (no separate Year entity).
/// </summary>
public class Package : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public string TargetGrade { get; set; } = string.Empty;

    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public ICollection<Term> Terms { get; set; } = new List<Term>();
}

public class ContentSection : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Order { get; set; }
    public decimal Price { get; set; }

    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}

public class Lesson : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int Order { get; set; }
    public decimal Price { get; set; }

    public Guid ContentSectionId { get; set; }
    public ContentSection ContentSection { get; set; } = null!;

    // Optional Exam associated with the lesson
    public Guid? ExamId { get; set; }

    public ICollection<LessonVideo> Videos { get; set; } = new List<LessonVideo>();
    public ICollection<LessonResource> Resources { get; set; } = new List<LessonResource>();
    public ICollection<LessonComment> Comments { get; set; } = new List<LessonComment>();
}

public class LessonVideo : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    // e.g., YouTube, Vimeo, custom
    public string Provider { get; set; } = string.Empty;
    public string ProviderVideoId { get; set; } = string.Empty;

    public int Order { get; set; }

    public int MaxWatchCount { get; set; } = 3; // Hard-lock limit

    /// <summary>Admin-assigned type/tag for the video</summary>
    public string? VideoTag { get; set; }

    public string? SubtitleUrl { get; set; }
    public bool IsProcessingAI { get; set; } = false;
    public bool IsProcessingMindmaps { get; set; } = false;

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    // Optional Exam associated directly with this video specific
    public Guid? ExamId { get; set; }
    public Exam? Exam { get; set; }

    public ICollection<VideoChapter> VideoChapters { get; set; } = new List<VideoChapter>();
}

public class LessonResource : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    // URL or file path
    public string FileUrl { get; set; } = string.Empty;

    // e.g., PDF, Image
    public string ResourceType { get; set; } = string.Empty;

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
}

public class LessonComment : BaseEntity
{
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public LessonCommentStatus Status { get; set; } = LessonCommentStatus.Pending;

    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
}
