using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class Program : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // e.g., "1st Secondary", "2nd Secondary", "3rd Secondary"
    public string TargetGrade { get; set; } = string.Empty; 

    public ICollection<Package> Packages { get; set; } = new List<Package>();
}

/// <summary>
/// Package represents the academic year.
/// Contains Terms directly (no separate Year entity).
/// </summary>
public class Package : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public ICollection<Term> Terms { get; set; } = new List<Term>();
}

public class ContentSection : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }

    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}

public class Lesson : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int Order { get; set; }
    
    public Guid ContentSectionId { get; set; }
    public ContentSection ContentSection { get; set; } = null!;

    // Optional Exam associated with the lesson
    public Guid? ExamId { get; set; }

    public ICollection<LessonVideo> Videos { get; set; } = new List<LessonVideo>();
    public ICollection<LessonResource> Resources { get; set; } = new List<LessonResource>();
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

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
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
