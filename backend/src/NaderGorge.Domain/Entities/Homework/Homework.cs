using System;
using System.Collections.Generic;

namespace NaderGorge.Domain.Entities.Homework;

public class Homework
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsMandatory { get; set; } = true;
    public decimal? PassingScoreThreshold { get; set; }
    public decimal TotalScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HomeworkQuestion> Questions { get; set; } = new List<HomeworkQuestion>();
    public ICollection<HomeworkSubmission> Submissions { get; set; } = new List<HomeworkSubmission>();
}
