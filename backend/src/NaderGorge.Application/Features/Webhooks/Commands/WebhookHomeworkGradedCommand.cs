using System.Data;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Webhooks.Commands;

public sealed record HomeworkAIGrade(Guid AnswerId, decimal Score, string Feedback);
public sealed record HomeworkGradingReceipt(Guid SubmissionId, string Status);
public sealed record WebhookHomeworkGradedCommand(Guid SubmissionId, string Fingerprint, HomeworkAIGrade[] Grades)
    : IRequest<ApiResponse<HomeworkGradingReceipt>>;

public sealed class WebhookHomeworkGradedCommandHandler(IAppDbContext db)
    : IRequestHandler<WebhookHomeworkGradedCommand, ApiResponse<HomeworkGradingReceipt>>
{
    public Task<ApiResponse<HomeworkGradingReceipt>> Handle(WebhookHomeworkGradedCommand request, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var response = await ApplyAsync(request, retryCt);
            if (response.Success) await transaction.CommitAsync(retryCt);
            return response;
        }, ct);

    private async Task<ApiResponse<HomeworkGradingReceipt>> ApplyAsync(WebhookHomeworkGradedCommand request, CancellationToken ct)
    {
        var submission = await db.HomeworkSubmissions.Include(s => s.Answers).SingleOrDefaultAsync(s => s.Id == request.SubmissionId, ct);
        if (submission is null) return Receipt(request.SubmissionId, "Deleted");
        if (submission.Status != SubmissionStatus.PendingReview || HomeworkEvaluationQueue.Fingerprint(submission) != request.Fingerprint)
            return Receipt(request.SubmissionId, "Superseded");
        var eligible = HomeworkEvaluationQueue.Questions(submission).Select(q => q.AnswerId).ToHashSet();
        if (request.Grades is null || request.Grades.Length == 0 || request.Grades.Length > 500
            || request.Grades.Select(g => g.AnswerId).Distinct().Count() != request.Grades.Length
            || !eligible.SetEquals(request.Grades.Select(g => g.AnswerId))
            || request.Grades.Any(g => g.Score is not (0m or 1m) || string.IsNullOrWhiteSpace(g.Feedback) || g.Feedback.Length > 4000))
            return ApiResponse<HomeworkGradingReceipt>.Fail("Invalid homework evaluation result.");

        var definition = AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson!, "homework", submission.HomeworkId);
        var questions = definition.Questions.ToDictionary(q => q.Id);
        foreach (var grade in request.Grades)
        {
            var answer = submission.Answers.Single(a => a.Id == grade.AnswerId);
            answer.ScoreReceived = grade.Score == 1 ? checked((int)questions[answer.QuestionId].Points) : 0;
        }
        definition = definition.WithGrades(submission.Answers.Where(a => eligible.Contains(a.Id))
            .ToDictionary(a => a.QuestionId, a => ((decimal)a.ScoreReceived!.Value, false)));
        Complete(submission, definition);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "HomeworkAIGraded", EntityType = "HomeworkSubmission", EntityId = submission.Id,
            CorrelationId = request.Fingerprint, NewValues = JsonSerializer.Serialize(request.Grades)
        });
        await db.SaveChangesAsync(ct);
        return Receipt(submission.Id, submission.Status.ToString());
    }

    private void Complete(HomeworkSubmission submission, AssessmentDefinitionSnapshot definition)
    {
        // Legacy zero-total assignments use their authored question points; the teacher's pass threshold is preserved.
        if (definition.TotalScore == 0) definition = definition with { TotalScore = definition.Questions.Sum(q => q.Points) };
        submission.DefinitionSnapshotJson = definition.ToJson();
        submission.TotalScoreSnapshot = definition.TotalScore;
        submission.PassingScoreSnapshot ??= definition.PassingScore ?? 0;
        if (submission.Answers.Any(a => a.ScoreReceived is null && definition.Questions.Any(q => q.Id == a.QuestionId)
                && definition.Revision?.Answers.Any(r => r.QuestionId == a.QuestionId && r.Excluded) != true)
            || definition.Revision?.RequiresReview == true || definition.Revision?.RequiresCompletion == true) return;
        submission.OverallScore = definition.Revision?.ScaledScore(definition.TotalScore)
            ?? GradingEvaluationService.CalculateScaledScore(submission.Answers.Sum(a => a.ScoreReceived ?? 0),
                definition.Questions.Sum(q => q.Points), definition.TotalScore);
        submission.Status = SubmissionStatus.Graded;
        submission.GradedAt = DateTime.UtcNow;
        submission.Evaluation = GradingEvaluationService.DetermineEvaluation(submission.OverallScore,
            submission.PassingScoreSnapshot.Value, definition.TotalScore);
        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "HomeworkGraded", TargetUserId = submission.StudentId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new { homeworkId = submission.HomeworkId, submissionId = submission.Id, score = submission.OverallScore })
        });
    }

    private static ApiResponse<HomeworkGradingReceipt> Receipt(Guid id, string status) => ApiResponse<HomeworkGradingReceipt>.Ok(new(id, status));
}
