using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Homework.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed record StartedHomeworkRevision(DateTime StartedAt, AssessmentDefinitionSnapshot Definition);

public sealed class HomeworkRevisionCompletion(IAppDbContext db)
{
    public Task<ApiResponse<StartedHomeworkRevision>> Start(Guid submissionId, Guid studentId, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var submission = await db.HomeworkSubmissions.SingleOrDefaultAsync(s => s.Id == submissionId && s.StudentId == studentId, retryCt);
            if (submission?.DefinitionSnapshotJson is null || submission.Status != SubmissionStatus.InProgress)
                return ApiResponse<StartedHomeworkRevision>.Fail("تغيّرت حالة المحاولة. أعد تحميل الواجب.");
            var definition = AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson, "homework", submission.HomeworkId);
            if (definition.Revision?.RequiresCompletion != true)
                return ApiResponse<StartedHomeworkRevision>.Fail("لا توجد أسئلة تحتاج استكمالًا في هذه المحاولة.");
            if (definition.CompletionStartedAt is null)
            {
                submission.StartedAt = DateTime.UtcNow;
                definition = definition with { CompletionStartedAt = submission.StartedAt };
                submission.DefinitionSnapshotJson = definition.ToJson();
                await db.SaveChangesAsync(retryCt);
            }
            await transaction.CommitAsync(retryCt);
            return ApiResponse<StartedHomeworkRevision>.Ok(new(submission.StartedAt, definition));
        }, ct);

    public Task<ApiResponse<bool>> Submit(SubmitHomeworkCommand request, Guid submissionId, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var submission = await db.HomeworkSubmissions.Include(s => s.Answers)
                .SingleOrDefaultAsync(s => s.Id == submissionId && s.StudentId == request.StudentId && s.HomeworkId == request.HomeworkId, retryCt);
            if (submission?.DefinitionSnapshotJson is null || submission.Status != SubmissionStatus.InProgress)
                return ApiResponse<bool>.Fail("تم تسليم هذه المحاولة بالفعل أو تغيّرت حالتها.");
            var definition = AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson, "homework", submission.HomeworkId);
            if (definition.Revision?.RequiresCompletion != true || definition.CompletionStartedAt is null
                || request.RevisionId is null || request.RevisionId != definition.RevisionId)
                return ApiResponse<bool>.Fail("أعد فتح الواجب لتحميل الأسئلة المضافة قبل إرسال الإجابات.");
            var required = definition.Revision.Answers.Where(a => a.RequiresCompletion && !a.Excluded).Select(a => a.QuestionId).ToHashSet();
            if (request.Answers.Select(a => a.QuestionId).Distinct().Count() != request.Answers.Count
                || request.Answers.Any(a => !required.Contains(a.QuestionId)))
                return ApiResponse<bool>.Fail("يمكن إرسال إجابات الأسئلة المطلوبة للاستكمال فقط؛ الإجابات القديمة محفوظة.");
            var previousJson = submission.DefinitionSnapshotJson;
            CompleteAnswers(submission, definition, request);
            db.AuditLogs.Add(new AuditLog
            {
                Action = "HomeworkRevisionCompleted", EntityType = "Homework", EntityId = submission.Id,
                PerformedByUserId = request.StudentId, CorrelationId = request.RevisionId.ToString(),
                OldValues = previousJson, NewValues = submission.DefinitionSnapshotJson
            });
            db.OutboxEvents.Add(new OutboxEvent
            {
                Type = "HomeworkSubmitted", TargetUserId = submission.StudentId.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { homeworkId = submission.HomeworkId, submissionId = submission.Id, studentId = submission.StudentId })
            });
            await db.SaveChangesAsync(retryCt);
            await transaction.CommitAsync(retryCt);
            return ApiResponse<bool>.Ok(true);
        }, ct);

    private void CompleteAnswers(HomeworkSubmission submission, AssessmentDefinitionSnapshot definition, SubmitHomeworkCommand request)
    {
        var provided = request.Answers.ToDictionary(a => a.QuestionId);
        var questions = definition.Questions.ToDictionary(q => q.Id);
        var expired = definition.DurationMinutes is int minutes && DateTime.UtcNow - definition.CompletionStartedAt!.Value
            > TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(60));
        var updated = definition.Revision!.Answers.Select(grade =>
        {
            if (!grade.RequiresCompletion || grade.Excluded) return grade;
            var answer = submission.Answers.SingleOrDefault(a => a.QuestionId == grade.QuestionId);
            if (answer is null)
            {
                answer = new HomeworkAnswer { HomeworkSubmissionId = submission.Id, QuestionId = grade.QuestionId };
                submission.Answers.Add(answer);
                db.HomeworkAnswers.Add(answer);
            }
            answer.ProvidedAnswer = provided.GetValueOrDefault(grade.QuestionId)?.ProvidedAnswer ?? string.Empty;
            var points = expired || string.IsNullOrWhiteSpace(answer.ProvidedAnswer) ? 0
                : AssessmentAttemptRegrader.AutomaticGrade(questions[grade.QuestionId],
                    new(grade.QuestionId, answer.ProvidedAnswer, null, null, false), "homework");
            answer.ScoreReceived = points is null ? null : checked((int)points.Value);
            return grade with { RequiresCompletion = false, AwardedPoints = points, ManuallyGraded = false };
        }).ToArray();
        var revision = definition.Revision! with { Answers = updated };
        submission.DefinitionSnapshotJson = (definition with { Revision = revision }).ToJson();
        submission.OverallScore = revision.ScaledScore(definition.TotalScore);
        submission.Status = revision.RequiresReview ? SubmissionStatus.PendingReview : SubmissionStatus.Graded;
        submission.Evaluation = revision.RequiresReview ? "قيد التصحيح"
            : GradingEvaluationService.DetermineEvaluation(submission.OverallScore, definition.PassingScore ?? 0, definition.TotalScore);
        submission.SubmittedAt = DateTime.UtcNow;
        submission.GradedAt = revision.RequiresReview ? null : DateTime.UtcNow;
    }
}
