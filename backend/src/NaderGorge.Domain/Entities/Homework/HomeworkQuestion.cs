using System;

namespace NaderGorge.Domain.Entities.Homework;

public enum QuestionType
{
    MCQ,
    Essay
}

public class HomeworkQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HomeworkId { get; set; }
    public int Order { get; set; }
    public QuestionType QuestionType { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public string[]? PossibleAnswers { get; set; }
    public string? CorrectAnswerKey { get; set; }
    public int PointsActive { get; set; }

    public Homework Homework { get; set; } = null!;
}
