using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed record HomeworkEvaluationQuestion(Guid AnswerId, string QuestionText, string AnswerText, string ExpectedAnswer);
public sealed record HomeworkEvaluationPayload(Guid SubmissionId, string Fingerprint, HomeworkEvaluationQuestion[] Questions);

public static class HomeworkEvaluationQueue
{
    public const string EventType = "HomeworkEvaluationQueued";

    public static string Fingerprint(HomeworkSubmission submission) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            submission.Id, Definition = submission.DefinitionSnapshotJson is null ? null
                : AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson, "homework", submission.HomeworkId),
            Answers = submission.Answers.OrderBy(a => a.Id).Select(a => new { a.Id, a.QuestionId, a.ProvidedAnswer, a.ScoreReceived })
        }))));

    public static HomeworkEvaluationQuestion[] Questions(HomeworkSubmission submission)
    {
        if (submission.DefinitionSnapshotJson is null) return [];
        var definition = AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson, "homework", submission.HomeworkId);
        if (definition.Revision?.RequiresCompletion == true) return [];
        return definition.Questions.Where(q => q.Type == (int)NaderGorge.Domain.Entities.Homework.QuestionType.Essay
            && !string.IsNullOrWhiteSpace(q.Text)
            && definition.Revision?.Answers.Any(a => a.QuestionId == q.Id && a.Excluded) != true)
            .Join(submission.Answers.Where(a => a.ScoreReceived is null && !a.ProvidedAnswer.StartsWith("/uploads/audio/", StringComparison.Ordinal)),
                q => q.Id, a => a.QuestionId,
                (q, a) => new HomeworkEvaluationQuestion(a.Id, q.Text, a.ProvidedAnswer, q.WrittenCorrection ?? ""))
            .ToArray();
    }

    public static void Enqueue(IAppDbContext db, HomeworkSubmission submission)
    {
        if (submission.Status != SubmissionStatus.PendingReview) return;
        var questions = Questions(submission);
        if (questions.Length == 0) return;
        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = EventType, TargetGroup = submission.Id.ToString(),
            PayloadJson = JsonSerializer.Serialize(new HomeworkEvaluationPayload(submission.Id, Fingerprint(submission), questions))
        });
    }
}
