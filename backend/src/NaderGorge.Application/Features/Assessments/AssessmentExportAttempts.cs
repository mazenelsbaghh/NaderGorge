using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

internal sealed class AssessmentExportAttempts(IAppDbContext db)
{
    public async Task<IReadOnlyList<AssessmentExportRow>> ReadAsync(AssessmentTarget target, CancellationToken ct)
    {
        var rows = target.Kind == AssessmentKind.Exam ? await ExamsAsync(target.AssessmentId, ct)
            : await HomeworksAsync(target.AssessmentId, ct);
        if (rows.Count > GetAssessmentExportQueryHandler.RowLimit)
            throw new ArgumentException("يتجاوز التقرير 50000 محاولة. لم يتم تنزيل ملف ناقص؛ تواصل مع الدعم لتجهيز الكشف الكامل.");
        return rows.OrderBy(r => r.Student.Name).ThenBy(r => r.Student.Id)
            .ThenByDescending(r => r.SubmittedAt).ThenBy(r => r.AttemptId).ToArray();
    }

    private async Task<List<AssessmentExportRow>> ExamsAsync(Guid id, CancellationToken ct)
    {
        var attempts = db.StudentExamAttempts.AsNoTracking().Where(a => a.ExamId == id &&
                (a.SubmittedAt != null || a.Evaluation != null || a.IsPassed || db.EssaySubmissions.Any(e => e.StudentExamAttemptId == a.Id)))
            .OrderBy(a => a.Id).Take(GetAssessmentExportQueryHandler.RowLimit + 1)
            .Select(a => new ExamExportAttempt(a.Id,
                new(a.UserId, a.User.FullName, a.User.PhoneNumber, a.User.StudentProfile == null ? null : a.User.StudentProfile.StudentCode,
                    a.User.StudentProfile == null ? null : a.User.StudentProfile.ParentPhone,
                    a.User.StudentProfile == null ? null : a.User.StudentProfile.MotherPhone),
                a.DefinitionSnapshotJson, a.ScoreAchieved, a.IsPassed, a.IsTimeExpired,
                a.Answers.Where(answer => !answer.IsExcludedFromScore).Sum(answer => answer.PointsAwarded),
                a.StartedAt, a.SubmittedAt, a.Evaluation,
                db.EssaySubmissions.Any(e => e.StudentExamAttemptId == a.Id &&
                    (e.Status == EssaySubmissionStatus.WaitAI || e.Status == EssaySubmissionStatus.WaitTeacher))));
        var rows = new List<AssessmentExportRow>();
        await foreach (var attempt in attempts.AsAsyncEnumerable().WithCancellation(ct))
            rows.Add(ExamRow(attempt, id));
        return rows;
    }

    private static AssessmentExportRow ExamRow(ExamExportAttempt attempt, Guid examId)
    {
        if (attempt.Snapshot is null)
            return new(attempt.Student, attempt.Id, attempt.Score, null, "يحتاج تسوية سلم الدرجات",
                attempt.Evaluation, attempt.StartedAt, attempt.SubmittedAt, false);
        var scale = AssessmentAttemptScaleNormalizer.Project(attempt.Snapshot, examId, attempt.Score,
            attempt.Passed, attempt.Expired, attempt.AwardedPoints);
        var incomplete = scale.Definition.Revision?.RequiresCompletion == true;
        var pending = attempt.PendingEssay && scale.Definition.EffectiveEssayGradingMode != ExamEssayGradingMode.ModelAnswerOnly;
        var status = incomplete ? "يحتاج استكمال" : pending ? "بانتظار التصحيح" : "تم التصحيح";
        return new(attempt.Student, attempt.Id, scale.ScoreAchieved, scale.Definition.TotalScore,
            status, attempt.Evaluation, attempt.StartedAt, attempt.SubmittedAt, !incomplete && !pending);
    }

    private async Task<List<AssessmentExportRow>> HomeworksAsync(Guid id, CancellationToken ct)
    {
        var attempts = db.HomeworkSubmissions.AsNoTracking().Where(s => s.HomeworkId == id &&
                (s.SubmittedAt != null || s.Status == SubmissionStatus.Graded || s.Status == SubmissionStatus.PendingReview))
            .OrderBy(s => s.Id).Take(GetAssessmentExportQueryHandler.RowLimit + 1)
            .Select(s => new HomeworkExportAttempt(s.Id,
                new(s.StudentId, s.Student.FullName, s.Student.PhoneNumber, s.Student.StudentProfile == null ? null : s.Student.StudentProfile.StudentCode,
                    s.Student.StudentProfile == null ? null : s.Student.StudentProfile.ParentPhone,
                    s.Student.StudentProfile == null ? null : s.Student.StudentProfile.MotherPhone),
                s.DefinitionSnapshotJson, s.TotalScoreSnapshot, s.OverallScore, s.Status,
                s.StartedAt, s.SubmittedAt, s.Evaluation));
        var rows = new List<AssessmentExportRow>();
        await foreach (var attempt in attempts.AsAsyncEnumerable().WithCancellation(ct))
            rows.Add(HomeworkRow(attempt, id));
        return rows;
    }

    private static AssessmentExportRow HomeworkRow(HomeworkExportAttempt attempt, Guid id)
    {
        var definition = attempt.Snapshot is null ? null : AssessmentDefinitionSnapshot.Read(attempt.Snapshot, "homework", id);
        var maximum = attempt.Maximum ?? definition?.TotalScore;
        var final = attempt.Status == SubmissionStatus.Graded && maximum.HasValue && definition?.Revision?.RequiresCompletion != true;
        var status = maximum is null ? "يحتاج تسوية سلم الدرجات" : definition?.Revision?.RequiresCompletion == true
            ? "يحتاج استكمال" : final ? "تم التصحيح" : "بانتظار التصحيح";
        return new(attempt.Student, attempt.Id, attempt.Score, maximum, status, attempt.Evaluation,
            attempt.StartedAt, attempt.SubmittedAt, final);
    }

    private sealed record ExamExportAttempt(Guid Id, AssessmentExportStudent Student, string? Snapshot,
        decimal Score, bool Passed, bool Expired, decimal AwardedPoints, DateTime? StartedAt,
        DateTime? SubmittedAt, string? Evaluation, bool PendingEssay);
    private sealed record HomeworkExportAttempt(Guid Id, AssessmentExportStudent Student, string? Snapshot,
        decimal? Maximum, decimal Score, SubmissionStatus Status, DateTime StartedAt, DateTime? SubmittedAt, string? Evaluation);
}
