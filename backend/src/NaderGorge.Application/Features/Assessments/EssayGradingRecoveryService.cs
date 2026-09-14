using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed class EssayGradingRecoveryService(IAppDbContext db)
{
    public Task<List<Guid>> FindDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.Subtract(EssayEvaluationQueue.RecoveryDelay);
        return db.EssaySubmissions.AsNoTracking()
            .Where(e => e.Status == EssaySubmissionStatus.WaitAI
                && (e.AiNextRetryAt <= now || (e.AiNextRetryAt == null && e.CreatedAt <= cutoff)))
            .OrderBy(e => e.AiNextRetryAt ?? e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => e.Id).Take(100).ToListAsync(ct);
    }

    public Task<bool> RecoverAsync(Guid essayId, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var essay = await db.EssaySubmissions.Include(e => e.Attempt)
                .SingleOrDefaultAsync(e => e.Id == essayId, retryCt);
            if (essay is null || essay.Status != EssaySubmissionStatus.WaitAI
                || (essay.AiNextRetryAt ?? essay.CreatedAt.Add(EssayEvaluationQueue.RecoveryDelay)) > DateTime.UtcNow)
                return false;
            var exam = await db.Exams.Include(e => e.ExamQuestions).ThenInclude(q => q.Question)
                .SingleAsync(e => e.Id == essay.Attempt.ExamId, retryCt);
            var definition = AssessmentDefinitionSnapshot.ResolveExam(exam, essay.Attempt.DefinitionSnapshotJson);
            var question = definition.ExamQuestions.SingleOrDefault(q => q.QuestionBankItemId == essay.QuestionId);
            if (question is null)
                essay.Status = EssaySubmissionStatus.WaitTeacher;
            else
                EssayEvaluationQueue.Enqueue(db, essay, question.Question.Text, question.Question.WrittenCorrection);
            await db.SaveChangesAsync(retryCt);
            await transaction.CommitAsync(retryCt);
            return true;
        }, ct);

    public Task<int> DeferFailureAsync(Guid essayId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.EssaySubmissions.Where(e => e.Id == essayId && e.Status == EssaySubmissionStatus.WaitAI
                && (e.AiNextRetryAt == null || e.AiNextRetryAt <= now))
            .ExecuteUpdateAsync(update => update.SetProperty(e => e.AiNextRetryAt,
                now.Add(EssayEvaluationQueue.RecoveryDelay)), ct);
    }
}
