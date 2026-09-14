using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

internal sealed class AdminAIStudentAssessmentSnapshotReader(IAppDbContext db)
{
    public async Task<AdminAIStudentSnapshotSection<AdminAIStudentAssessmentsSection>> LoadAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var exams = request.AssessmentFields.Contains("exams")
            ? await LoadExamAssessmentsAsync(request, ct)
            : null;
        var homework = request.AssessmentFields.Contains("homework")
            ? await LoadHomeworkAssessmentsAsync(request, ct)
            : null;
        var essays = request.AssessmentFields.Contains("essays")
            ? await LoadEssayAssessmentsAsync(request.StudentId, ct)
            : null;
        var assessments = new AdminAIStudentAssessmentsSection(exams?.Payload, homework?.Payload, essays);
        var isTruncated = exams?.IsTruncated == true || homework?.IsTruncated == true;
        return new(assessments, isTruncated);
    }

    private async Task<AdminAIStudentSnapshotSection<AdminAIStudentExamAssessments>> LoadExamAssessmentsAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var counts = await LoadExamCountsAsync(request.StudentId, ct);
        var recentAttempts = await LoadRecentExamsAsync(request, ct);
        var exams = new AdminAIStudentExamAssessments(
            counts.Total,
            counts.DistinctExams,
            counts.Passed,
            counts.Failed,
            counts.InProgress,
            counts.PendingGrading,
            counts.TimedOut,
            counts.DistinctPassedExams,
            counts.DistinctFailedExams,
            recentAttempts.Take(request.RecentLimit).ToArray(),
            true,
            true);
        return new(exams, recentAttempts.Count > request.RecentLimit);
    }

    private async Task<ExamCounts> LoadExamCountsAsync(
        Guid studentId,
        CancellationToken ct)
    {
        var counts = await db.StudentExamAttempts.AsNoTracking()
            .Where(attempt => attempt.UserId == studentId)
            .GroupBy(_ => 1)
            .Select(group => new ExamCounts(
                group.Count(),
                group.Select(attempt => attempt.ExamId).Distinct().Count(),
                group.Count(attempt =>
                    !attempt.IsTimeExpired &&
                    attempt.Evaluation != "انتهى الوقت" &&
                    attempt.Evaluation != null &&
                    attempt.Evaluation != "قيد التصحيح" &&
                    attempt.IsPassed),
                group.Count(attempt =>
                    !attempt.IsTimeExpired &&
                    attempt.Evaluation != "انتهى الوقت" &&
                    attempt.Evaluation != null &&
                    attempt.Evaluation != "قيد التصحيح" &&
                    !attempt.IsPassed),
                group.Count(attempt =>
                    !attempt.IsTimeExpired &&
                    attempt.Evaluation != "انتهى الوقت" &&
                    attempt.Evaluation == null),
                group.Count(attempt =>
                    !attempt.IsTimeExpired &&
                    attempt.Evaluation != "انتهى الوقت" &&
                    attempt.Evaluation == "قيد التصحيح"),
                group.Count(attempt => attempt.IsTimeExpired || attempt.Evaluation == "انتهى الوقت"),
                group.Where(attempt =>
                        !attempt.IsTimeExpired &&
                        attempt.Evaluation != "انتهى الوقت" &&
                        attempt.Evaluation != null &&
                        attempt.Evaluation != "قيد التصحيح" &&
                        attempt.IsPassed)
                    .Select(attempt => attempt.ExamId)
                    .Distinct()
                    .Count(),
                group.Where(attempt =>
                        attempt.IsTimeExpired ||
                        attempt.Evaluation == "انتهى الوقت" ||
                        (attempt.Evaluation != null &&
                         attempt.Evaluation != "قيد التصحيح" &&
                         !attempt.IsPassed))
                    .Select(attempt => attempt.ExamId)
                    .Distinct()
                    .Count()))
            .SingleOrDefaultAsync(ct);
        return counts ?? new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private async Task<IReadOnlyList<AdminAIStudentExamAttemptItem>> LoadRecentExamsAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        if (request.RecentLimit == 0)
            return [];

        var attempts = await db.StudentExamAttempts.AsNoTracking()
            .Where(attempt => attempt.UserId == request.StudentId)
            .OrderByDescending(attempt => attempt.StartedAt ?? attempt.CreatedAt)
            .ThenByDescending(attempt => attempt.Id)
            .Take(request.RecentLimit + 1)
            .Select(attempt => new RecentExamProjection(
                attempt.ExamId,
                attempt.Exam.Title,
                attempt.Exam.CreatedByTeacherId,
                attempt.Exam.CreatedByTeacher.User.FullName,
                attempt.ScoreAchieved,
                attempt.Exam.TotalScore,
                attempt.IsPassed,
                attempt.IsTimeExpired,
                attempt.Evaluation,
                attempt.StartedAt ?? attempt.CreatedAt))
            .ToArrayAsync(ct);
        return attempts.Select(MapRecentExam).ToArray();
    }

    private static AdminAIStudentExamAttemptItem MapRecentExam(RecentExamProjection attempt) =>
        new(
            attempt.ExamId,
            AdminAIReadArguments.SafeText(attempt.ExamTitle, 160),
            attempt.TeacherId,
            AdminAIReadArguments.SafeText(attempt.TeacherName, 120),
            attempt.ScoreAchieved,
            attempt.CurrentTotalScore,
            StateName(ClassifyAttempt(attempt)),
            attempt.AttemptedAt);

    private async Task<AdminAIStudentSnapshotSection<AdminAIStudentHomeworkAssessments>> LoadHomeworkAssessmentsAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var counts = await db.HomeworkSubmissions.AsNoTracking()
            .Where(submission => submission.StudentId == request.StudentId)
            .GroupBy(_ => 1)
            .Select(group => new HomeworkCounts(
                group.Count(),
                group.Count(submission =>
                    submission.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded),
                group.Count(submission =>
                    submission.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Missed)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0);
        var recentHomework = await LoadRecentHomeworkAsync(request, ct);
        var homework = new AdminAIStudentHomeworkAssessments(
            counts.Total,
            counts.Graded,
            counts.Missed,
            recentHomework.Take(request.RecentLimit).ToArray());
        return new(homework, recentHomework.Count > request.RecentLimit);
    }

    private async Task<IReadOnlyList<AdminAIStudentHomeworkItem>> LoadRecentHomeworkAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        if (request.RecentLimit == 0)
            return [];

        var homework = await (
                from submission in db.HomeworkSubmissions.AsNoTracking()
                join assignment in db.Homeworks.AsNoTracking() on submission.HomeworkId equals assignment.Id
                join lesson in db.Lessons.AsNoTracking() on assignment.LessonId equals lesson.Id
                where submission.StudentId == request.StudentId
                orderby (submission.SubmittedAt ?? submission.StartedAt) descending, submission.Id descending
                select new AdminAIStudentHomeworkItem(
                    assignment.Id,
                    assignment.Title,
                    lesson.ContentSection.Term.Package.TeacherId,
                    lesson.ContentSection.Term.Package.Teacher.User.FullName,
                    submission.OverallScore,
                    assignment.TotalScore,
                    submission.Status.ToString(),
                    submission.SubmittedAt ?? submission.StartedAt))
            .Take(request.RecentLimit + 1)
            .ToArrayAsync(ct);
        return homework.Select(SanitizeHomework).ToArray();
    }

    private static AdminAIStudentHomeworkItem SanitizeHomework(AdminAIStudentHomeworkItem homework) => homework with
    {
        HomeworkTitle = AdminAIReadArguments.SafeText(homework.HomeworkTitle, 160),
        TeacherName = AdminAIReadArguments.SafeText(homework.TeacherName, 120)
    };

    private async Task<AdminAIStudentEssayAssessments> LoadEssayAssessmentsAsync(
        Guid studentId,
        CancellationToken ct)
    {
        var counts = await db.EssaySubmissions.AsNoTracking()
            .Where(submission => submission.StudentId == studentId)
            .GroupBy(_ => 1)
            .Select(group => new AdminAIStudentEssayAssessments(
                group.Count(),
                group.Count(submission =>
                    submission.Status == NaderGorge.Domain.Entities.EssaySubmissionStatus.WaitTeacher),
                group.Count(submission =>
                    submission.Status == NaderGorge.Domain.Entities.EssaySubmissionStatus.TeacherGraded)))
            .SingleOrDefaultAsync(ct);
        return counts ?? new(0, 0, 0);
    }

    private static ExamAttemptState ClassifyAttempt(IExamAttemptStateSource attempt) =>
        attempt.IsTimeExpired || string.Equals(attempt.Evaluation, "انتهى الوقت", StringComparison.Ordinal)
            ? ExamAttemptState.TimedOut
            : attempt.Evaluation is null
                ? ExamAttemptState.InProgress
                : string.Equals(attempt.Evaluation, "قيد التصحيح", StringComparison.Ordinal)
                    ? ExamAttemptState.PendingGrading
                    : attempt.IsPassed
                        ? ExamAttemptState.Passed
                        : ExamAttemptState.Failed;

    private static string StateName(ExamAttemptState state) => state switch
    {
        ExamAttemptState.InProgress => "in_progress",
        ExamAttemptState.PendingGrading => "pending_grading",
        ExamAttemptState.Passed => "passed",
        ExamAttemptState.Failed => "failed",
        ExamAttemptState.TimedOut => "timed_out",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    private interface IExamAttemptStateSource
    {
        bool IsPassed { get; }
        bool IsTimeExpired { get; }
        string? Evaluation { get; }
    }

    private sealed record RecentExamProjection(
        Guid ExamId,
        string ExamTitle,
        Guid TeacherId,
        string TeacherName,
        decimal ScoreAchieved,
        decimal CurrentTotalScore,
        bool IsPassed,
        bool IsTimeExpired,
        string? Evaluation,
        DateTime AttemptedAt) : IExamAttemptStateSource;

    private sealed record ExamCounts(
        int Total,
        int DistinctExams,
        int Passed,
        int Failed,
        int InProgress,
        int PendingGrading,
        int TimedOut,
        int DistinctPassedExams,
        int DistinctFailedExams);

    private sealed record HomeworkCounts(int Total, int Graded, int Missed);

    private enum ExamAttemptState
    {
        InProgress,
        PendingGrading,
        Passed,
        Failed,
        TimedOut
    }
}
