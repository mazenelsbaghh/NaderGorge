using System.Text.Json;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed class ExamPassingScoreReduction(IAppDbContext db)
{
    public void Apply(StudentExamAttempt attempt, IReadOnlyList<EssaySubmission> essays,
        SaveAssessmentRevisionCommand request)
    {
        var saved = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", attempt.ExamId);
        if (request.Definition.TotalScore <= 0 || saved.TotalScore <= 0) return;
        var threshold = Math.Round((request.Definition.PassingScore ?? 0) / request.Definition.TotalScore * saved.TotalScore, 2);
        if (threshold >= (saved.PassingScore ?? 0)) return;
        var before = Snapshot(attempt);
        var previouslyPassed = attempt.IsPassed;
        attempt.DefinitionSnapshotJson = (saved with { PassingScore = threshold }).ToJson();
        if (CanFinalize(attempt, saved, essays) && attempt.ScoreAchieved >= threshold)
        {
            attempt.IsPassed = true;
            attempt.Evaluation = GradingEvaluationService.DetermineEvaluation(attempt.ScoreAchieved, threshold, saved.TotalScore);
        }
        Audit(attempt, before, request);
        if (!previouslyPassed && attempt.IsPassed) AnnounceResult(attempt);
    }

    private static bool CanFinalize(StudentExamAttempt attempt, AssessmentDefinitionSnapshot saved,
        IReadOnlyList<EssaySubmission> essays) =>
        !attempt.IsTimeExpired && attempt.Evaluation is not (null or "قيد التصحيح")
        && saved.Revision?.RequiresCompletion != true && saved.Revision?.RequiresReview != true
        && !essays.Any(essay => essay.Status != EssaySubmissionStatus.TeacherGraded);

    private static string Snapshot(StudentExamAttempt attempt) => JsonSerializer.Serialize(new
    {
        attempt.DefinitionSnapshotJson, attempt.ScoreAchieved, attempt.IsPassed, attempt.Evaluation
    });

    private void Audit(StudentExamAttempt attempt, string before, SaveAssessmentRevisionCommand request)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = "ExamPassingScoreReduced", EntityType = nameof(StudentExamAttempt), EntityId = attempt.Id,
            PerformedByUserId = request.Target.ActorId, CorrelationId = request.OperationId.ToString(),
            OldValues = before, NewValues = Snapshot(attempt),
            Reason = "تطبيق خفض نسبة النجاح على المحاولة السابقة مع الاحتفاظ بدرجتها وإجاباتها وحالة التصحيح"
        });
    }

    private void AnnounceResult(StudentExamAttempt attempt) => db.OutboxEvents.Add(new OutboxEvent
    {
        Type = "ExamResultReady", TargetUserId = attempt.UserId.ToString(),
        PayloadJson = JsonSerializer.Serialize(new
        {
            examId = attempt.ExamId, attemptId = attempt.Id, isPassed = attempt.IsPassed, score = attempt.ScoreAchieved
        })
    });
}
