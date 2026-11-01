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
    public decimal? PreciseScoreReceived { get; set; }
    public string? GradingFeedback { get; set; }

    // Retain the integer column so existing releases can read whole-point grades.
    public decimal? AwardedScore
    {
        get => PreciseScoreReceived ?? ScoreReceived;
        set
        {
            PreciseScoreReceived = value;
            ScoreReceived = value is decimal score && score == decimal.Truncate(score)
                ? checked((int)score) : null;
        }
    }

    public HomeworkSubmission Submission { get; set; } = null!;
    public HomeworkQuestion Question { get; set; } = null!;
}
