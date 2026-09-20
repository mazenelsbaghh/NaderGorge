using System.Text.Json;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public static class EssayEvaluationQueue
{
    public static readonly TimeSpan RecoveryDelay = TimeSpan.FromMinutes(10);

    public static void Enqueue(IAppDbContext db, EssaySubmission essay, string questionText, string? expectedAnswer)
    {
        if (essay.Status != EssaySubmissionStatus.WaitAI) return;
        // Audio still needs a teacher. Text answers can be graded from the question
        // itself when a legacy question has no authored answer key.
        if (!string.IsNullOrWhiteSpace(essay.AudioUrl) || string.IsNullOrWhiteSpace(questionText))
        {
            essay.Status = EssaySubmissionStatus.WaitTeacher;
            essay.AiNextRetryAt = null;
            return;
        }
        essay.AiNextRetryAt = DateTime.UtcNow.Add(RecoveryDelay);
        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "EssayEvaluationQueued",
            PayloadJson = JsonSerializer.Serialize(new
            {
                essaySubmissionId = essay.Id, questionId = essay.QuestionId, studentId = essay.StudentId,
                questionText, answerText = essay.AnswerText,
                expectedAnswer = expectedAnswer?.Trim() ?? string.Empty
            })
        });
    }
}
