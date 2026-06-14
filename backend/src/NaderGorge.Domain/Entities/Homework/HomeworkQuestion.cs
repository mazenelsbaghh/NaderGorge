using System;

namespace NaderGorge.Domain.Entities.Homework;

public enum QuestionType
{
    MCQ = 0,
    Essay = 1,
    FindTheMistake = 2
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

    public string? AudioUrl { get; set; }
    public string? WrittenCorrection { get; set; }
    public string? HintText { get; set; }
    public string? BaseText { get; set; }
    public int? MistakeStartIndex { get; set; }
    public int? MistakeEndIndex { get; set; }

    public Homework Homework { get; set; } = null!;
}
