using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed class HomeworkGradingRecoveryService(IAppDbContext db)
{
    public Task<List<Guid>> FindBatchAsync(Guid afterId, CancellationToken ct) => db.HomeworkSubmissions.AsNoTracking()
        .Where(s => s.Status == SubmissionStatus.PendingReview && s.DefinitionSnapshotJson != null && s.Id.CompareTo(afterId) > 0)
        .OrderBy(s => s.Id).Select(s => s.Id).Take(25).ToListAsync(ct);

    public Task<bool> RecoverAsync(Guid submissionId, CancellationToken ct) => SerializationRetryHelper.ExecuteAsync(async retryCt =>
    {
        db.ClearTrackedChanges();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var group = submissionId.ToString();
        if (await db.OutboxEvents.AnyAsync(e => e.Type == HomeworkEvaluationQueue.EventType && e.TargetGroup == group
            && (e.CreatedAt > cutoff || (e.ProcessedAt == null && !e.IsDeadLetter)), retryCt)) return false;
        var submission = await db.HomeworkSubmissions.Include(s => s.Answers).SingleOrDefaultAsync(s => s.Id == submissionId, retryCt);
        if (submission is null || submission.Status != SubmissionStatus.PendingReview || HomeworkEvaluationQueue.Questions(submission).Length == 0) return false;
        HomeworkEvaluationQueue.Enqueue(db, submission);
        await db.SaveChangesAsync(retryCt);
        await transaction.CommitAsync(retryCt);
        return true;
    }, ct);
}
