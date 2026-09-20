using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed class EssayGradingRecoveryService(IAppDbContext db)
{
    private const int RecoveryBatchSize = 200;

    public async Task<List<Guid>> FindDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.Subtract(EssayEvaluationQueue.RecoveryDelay);
        var due = await db.EssaySubmissions.AsNoTracking()
            .Where(e => e.Status == EssaySubmissionStatus.WaitAI
                && (e.AiNextRetryAt <= now || (e.AiNextRetryAt == null && e.CreatedAt <= cutoff)))
            .OrderBy(e => e.AiNextRetryAt ?? e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => e.Id).Take(100).ToListAsync(ct);
        if (due.Count == 100) return due;

        // Earlier releases sent text-only legacy questions without an answer key
        // to manual review. Reconsider only untouched submissions; audio and any
        // manually or previously AI-scored answer stay with the teacher.
        due.AddRange(await FindRecoverableLegacyAsync(cutoff, 100 - due.Count, ct));
        return due;
    }

    public Task<bool> RecoverAsync(Guid essayId, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var essay = await db.EssaySubmissions.Include(e => e.Attempt)
                .SingleOrDefaultAsync(e => e.Id == essayId, retryCt);
            if (essay is null)
                return false;
            var now = DateTime.UtcNow;
            var waitAiDue = essay.Status == EssaySubmissionStatus.WaitAI
                && (essay.AiNextRetryAt ?? essay.CreatedAt.Add(EssayEvaluationQueue.RecoveryDelay)) <= now;
            var legacyTeacherDue = essay.Status == EssaySubmissionStatus.WaitTeacher
                && string.IsNullOrWhiteSpace(essay.AudioUrl)
                && essay.AiInitialScore == null
                && essay.TeacherFinalScore == null
                && essay.CreatedAt.Add(EssayEvaluationQueue.RecoveryDelay) <= now;
            if (!waitAiDue && !legacyTeacherDue) return false;
            var exam = await db.Exams.Include(e => e.ExamQuestions).ThenInclude(q => q.Question)
                .SingleAsync(e => e.Id == essay.Attempt.ExamId, retryCt);
            var definition = AssessmentDefinitionSnapshot.ResolveExam(exam, essay.Attempt.DefinitionSnapshotJson);
            var question = definition.ExamQuestions.SingleOrDefault(q => q.QuestionBankItemId == essay.QuestionId);
            if (question is null)
            {
                essay.Status = EssaySubmissionStatus.WaitTeacher;
                essay.AiNextRetryAt = null;
            }
            else
            {
                if (legacyTeacherDue)
                {
                    var snapshot = essay.Attempt.DefinitionSnapshotJson is null ? null
                        : AssessmentDefinitionSnapshot.Read(essay.Attempt.DefinitionSnapshotJson, "exam", essay.Attempt.ExamId);
                    if (snapshot?.Revision?.RequiresReview == true
                        || !string.IsNullOrWhiteSpace(question.Question.WrittenCorrection))
                        return false;
                    essay.Status = EssaySubmissionStatus.WaitAI;
                }
                EssayEvaluationQueue.Enqueue(db, essay, question.Question.Text, question.Question.WrittenCorrection);
            }
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

    private async Task<List<Guid>> FindRecoverableLegacyAsync(DateTime cutoff, int limit, CancellationToken ct)
    {
        var recoveredIds = new List<Guid>(limit);
        for (var offset = 0; recoveredIds.Count < limit; offset += RecoveryBatchSize)
        {
            var candidates = await LoadLegacyCandidatesAsync(cutoff, offset, ct);
            recoveredIds.AddRange(candidates.Where(CanRecoverLegacy).Select(candidate => candidate.Id)
                .Take(limit - recoveredIds.Count));
            if (candidates.Count < RecoveryBatchSize) break;
        }
        return recoveredIds;
    }

    private Task<List<LegacyCandidate>> LoadLegacyCandidatesAsync(DateTime cutoff, int offset, CancellationToken ct) =>
        db.EssaySubmissions.AsNoTracking()
            .Where(e => e.Status == EssaySubmissionStatus.WaitTeacher
                && (e.AudioUrl == null || e.AudioUrl.Trim() == string.Empty)
                && e.AiInitialScore == null
                && e.TeacherFinalScore == null
                && e.CreatedAt <= cutoff
                && (e.Question.WrittenCorrection == null || e.Question.WrittenCorrection.Trim() == string.Empty))
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Skip(offset).Take(RecoveryBatchSize)
            .Select(e => new LegacyCandidate(e.Id, e.QuestionId, e.Attempt.ExamId,
                e.Attempt.DefinitionSnapshotJson, e.Question.Text, e.Question.WrittenCorrection))
            .ToListAsync(ct);

    private static bool CanRecoverLegacy(LegacyCandidate candidate)
    {
        if (candidate.DefinitionSnapshotJson is null)
            return !string.IsNullOrWhiteSpace(candidate.CurrentQuestionText)
                && string.IsNullOrWhiteSpace(candidate.CurrentWrittenCorrection);

        try
        {
            var snapshot = AssessmentDefinitionSnapshot.Read(candidate.DefinitionSnapshotJson, "exam", candidate.ExamId);
            if (snapshot.Revision?.RequiresReview == true) return false;
            var question = snapshot.Questions.SingleOrDefault(q => q.BankQuestionId == candidate.QuestionId);
            return question is not null
                && !string.IsNullOrWhiteSpace(question.Text)
                && string.IsNullOrWhiteSpace(question.WrittenCorrection);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed record LegacyCandidate(Guid Id, Guid QuestionId, Guid ExamId, string? DefinitionSnapshotJson,
        string CurrentQuestionText, string? CurrentWrittenCorrection);
}
