using System;

namespace NaderGorge.Domain.Entities.Homework;

public class HomeworkAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HomeworkSubmissionId { get; set; }
    public Guid QuestionId { get; set; }

    // Either text block for essay or selected key for MCQ
    public string ProvidedAnswer { get; set; } = string.Empty;
    public int? ScoreReceived { get; set; }

    public HomeworkSubmission Submission { get; set; } = null!;
    public HomeworkQuestion Question { get; set; } = null!;
}
