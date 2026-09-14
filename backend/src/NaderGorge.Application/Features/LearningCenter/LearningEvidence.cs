using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LearningCenter;

public sealed record LearningAnswerEvidence(Guid QuestionId, Guid LessonId, string Concept, string Text,
    bool Correct, string? WrongOption, decimal Points, decimal Awarded);
public sealed record LearningAttemptEvidence(Guid Id, Guid StudentId, string StudentName, Guid ExamId,
    Guid PackageId, DateTime At, decimal Percent, string Definition, List<LearningAnswerEvidence> Answers);
public sealed record LearningEvidenceSet(List<LearningAttemptEvidence> Attempts, int Excluded);

public sealed class LearningEvidenceReader(IAppDbContext db)
{
    public async Task<LearningEvidenceSet> ReadAsync(Guid[] packageIds, int days, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var teachers = db.Packages.Where(p => packageIds.Contains(p.Id)).Select(p => p.TeacherId);
        var query = db.StudentExamAttempts.AsNoTracking().Where(a => a.CreatedAt >= since && a.CreatedAt <= DateTime.UtcNow && teachers.Contains(a.Exam.CreatedByTeacherId) &&
            (a.Exam.LessonVideo != null && packageIds.Contains(a.Exam.LessonVideo.Lesson.ContentSection.Term.PackageId) ||
             a.Exam.ExamQuestions.Any(q => q.Question.LearningLesson != null && packageIds.Contains(q.Question.LearningLesson.ContentSection.Term.PackageId))))
            .Where(a => a.User.IsActive && !a.User.IsDeleted);
        if (await query.CountAsync(ct) > 5000)
            throw new ArgumentException("حجم النتائج كبير. اختر كورسًا أو فترة أقصر من الفلاتر.");
        if (await db.StudentAnswers.CountAsync(a => query.Any(attempt => attempt.Id == a.StudentExamAttemptId), ct) > 50000)
            throw new ArgumentException("عدد الإجابات كبير. اختر كورسًا أو فترة أقصر من الفلاتر.");
        var attempts = await query.Include(a => a.User).Include(a => a.Exam).ThenInclude(e => e.LessonVideo)
            .ThenInclude(v => v!.Lesson).ThenInclude(l => l.ContentSection).ThenInclude(s => s.Term)
            .Include(a => a.Answers).ThenInclude(a => a.ExamQuestion).ThenInclude(q => q.Question).ThenInclude(q => q.LearningLesson)
            .ThenInclude(l => l!.ContentSection).ThenInclude(s => s.Term)
            .Include(a => a.Answers).ThenInclude(a => a.SelectedOption)
            .ToListAsync(ct);
        var attemptIds = attempts.Select(a => a.Id).ToArray();
        var pending = await db.EssaySubmissions.Where(e => attemptIds.Contains(e.StudentExamAttemptId) && e.Status != EssaySubmissionStatus.TeacherGraded)
            .Select(e => e.StudentExamAttemptId).Distinct().ToListAsync(ct);
        var pendingIds = pending.ToHashSet();
        var accepted = new List<LearningAttemptEvidence>();
        foreach (var attempt in attempts)
        {
            if (attempt.Evaluation is null || pendingIds.Contains(attempt.Id)) continue;
            var snapshot = attempt.DefinitionSnapshotJson is null ? null : AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", attempt.ExamId);
            if (snapshot?.Revision is { RequiresCompletion: true } or { RequiresReview: true }) continue;
            var evidence = ToEvidence(attempt, snapshot, packageIds);
            if (evidence is not null) accepted.Add(evidence);
        }
        return new(accepted, attempts.Count - accepted.Count);
    }

    private static LearningAttemptEvidence? ToEvidence(StudentExamAttempt attempt, AssessmentDefinitionSnapshot? snapshot, Guid[] packageIds)
    {
        var total = snapshot?.TotalScore ?? attempt.Exam.TotalScore;
        if (total <= 0 || attempt.ScoreAchieved < 0 || attempt.ScoreAchieved > total) return null;
        var answers = attempt.Answers.Select(answer => ToAnswer(answer, snapshot)).Where(a => a is not null).Cast<LearningAnswerEvidence>().ToList();
        var packageId = attempt.Exam.LessonVideo?.Lesson.ContentSection.Term.PackageId
            ?? attempt.Answers.Select(a => a.ExamQuestion.Question.LearningLesson?.ContentSection.Term.PackageId).FirstOrDefault(id => id.HasValue);
        if (!packageId.HasValue || !packageIds.Contains(packageId.Value)) return null;
        // Legacy attempts have no immutable definition, so their scores cannot establish a comparable trend.
        var definition = snapshot is null ? "legacy:" + attempt.Id : total + ":" +
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(snapshot.Questions.OrderBy(q => q.Id)))));
        return new(attempt.Id, attempt.UserId, attempt.User.FullName, attempt.ExamId, packageId.Value,
            attempt.CreatedAt, Math.Round(attempt.ScoreAchieved / total * 100, 1), definition, answers);
    }

    private static LearningAnswerEvidence? ToAnswer(StudentAnswer answer, AssessmentDefinitionSnapshot? snapshot)
    {
        var question = answer.ExamQuestion.Question;
        if (!question.LearningLessonId.HasValue || string.IsNullOrWhiteSpace(question.LearningConcept)) return null;
        var original = snapshot?.Questions.SingleOrDefault(q => q.Id == answer.ExamQuestionId);
        if (snapshot is not null && original is null) return null;
        var wrong = answer.IsCorrect ? null : original?.Options.FirstOrDefault(o => o.Id == answer.SelectedOptionId)?.Text ?? answer.SelectedOption?.Text;
        return new(question.Id, question.LearningLessonId.Value, question.LearningConcept, original?.Text ?? question.Text,
            answer.IsCorrect, wrong, original?.Points ?? answer.ExamQuestion.Points, answer.PointsAwarded);
    }
}
