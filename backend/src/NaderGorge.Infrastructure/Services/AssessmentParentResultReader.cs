using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

internal sealed record AssessmentParentResult(
    string Kind, Guid AssessmentId, Guid AttemptId, User Student,
    AssessmentParentNotificationSettings Settings, DateTime? EnabledAt,
    string Title, decimal Score, decimal Total, string Evaluation, Guid? LessonId);

internal static class AssessmentParentResultReader
{
    public static async Task<AssessmentParentResult?> ReadAsync(IAppDbContext db, OutboxEvent notification, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(notification.PayloadJson);
        var kind = notification.Type == "ExamGraded" ? "exam" : "homework";
        if (!payload.RootElement.TryGetProperty(kind == "exam" ? "attemptId" : "submissionId", out var id)
            || !id.TryGetGuid(out var attemptId)) return null;
        var result = kind == "exam" ? await ReadExamAsync(db, attemptId, ct) : await ReadHomeworkAsync(db, attemptId, ct);
        return result is not null && result.Student.Id.ToString() == notification.TargetUserId
            && result.Student.IsActive && !result.Student.IsDeleted ? result : null;
    }

    private static async Task<AssessmentParentResult?> ReadHomeworkAsync(IAppDbContext db, Guid attemptId, CancellationToken ct)
    {
        var submission = await db.HomeworkSubmissions.AsNoTracking().Include(submission => submission.Homework)
            .Include(submission => submission.Student).ThenInclude(student => student.StudentProfile)
            .SingleOrDefaultAsync(submission => submission.Id == attemptId, ct);
        if (submission is null || submission.Status != SubmissionStatus.Graded || submission.SubmittedAt is null) return null;
        var definition = submission.DefinitionSnapshotJson is null ? null
            : AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson, "homework", submission.HomeworkId);
        if (definition?.Revision is { } revision && (revision.RequiresCompletion || revision.RequiresReview)) return null;
        return new("homework", submission.HomeworkId, submission.Id, submission.Student,
            AssessmentParentNotificationSettings.Read(submission.Homework.ParentNotificationSettingsJson),
            submission.Homework.ParentNotificationEnabledAt, definition?.Title ?? submission.Homework.Title,
            submission.OverallScore, definition?.TotalScore ?? submission.TotalScoreSnapshot ?? submission.Homework.TotalScore,
            submission.Evaluation ?? "تم التصحيح", submission.Homework.LessonId);
    }

    private static async Task<AssessmentParentResult?> ReadExamAsync(IAppDbContext db, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.StudentExamAttempts.AsNoTracking().Include(attempt => attempt.Exam)
            .Include(attempt => attempt.User).ThenInclude(student => student.StudentProfile)
            .SingleOrDefaultAsync(attempt => attempt.Id == attemptId, ct);
        if (attempt is null || string.IsNullOrWhiteSpace(attempt.Evaluation) || attempt.Evaluation == "قيد التصحيح") return null;
        var definition = attempt.DefinitionSnapshotJson is null ? null
            : AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", attempt.ExamId);
        if (definition?.Revision is { } revision && (revision.RequiresCompletion || revision.RequiresReview)) return null;
        var assignedQuestionIds = definition?.Questions.Select(question => question.BankQuestionId).ToArray();
        if (await db.EssaySubmissions.AnyAsync(essay => essay.StudentExamAttemptId == attemptId
            && (assignedQuestionIds == null || assignedQuestionIds.Contains(essay.QuestionId))
            && essay.Status != EssaySubmissionStatus.TeacherGraded, ct)) return null;
        var lessonId = await db.Lessons.Where(lesson => lesson.ExamId == attempt.ExamId)
            .Select(lesson => (Guid?)lesson.Id).FirstOrDefaultAsync(ct)
            ?? await db.LessonVideos.Where(video => video.ExamId == attempt.ExamId)
                .Select(video => (Guid?)video.LessonId).FirstOrDefaultAsync(ct);
        return new("exam", attempt.ExamId, attempt.Id, attempt.User,
            AssessmentParentNotificationSettings.Read(attempt.Exam.ParentNotificationSettingsJson),
            attempt.Exam.ParentNotificationEnabledAt, definition?.Title ?? attempt.Exam.Title,
            attempt.ScoreAchieved, definition?.TotalScore ?? attempt.Exam.TotalScore, attempt.Evaluation, lessonId);
    }

    public static async Task<string[]> ParametersAsync(IAppDbContext db, AssessmentParentResult result, CancellationToken ct)
    {
        var lesson = await db.Lessons.AsNoTracking().Where(lesson => lesson.Id == result.LessonId)
            .Select(lesson => new { lesson.Title, Subject = lesson.ContentSection.Term.Package.Subject.Name,
                Teacher = lesson.ContentSection.Term.Package.Teacher.User.FullName }).SingleOrDefaultAsync(ct);
        return result.Settings.Parameters.Select(parameter => parameter.Source switch
        {
            "ParentName" => $"ولي أمر {result.Student.FullName}",
            "StudentName" => result.Student.FullName,
            "ParentTrackingCode" => result.Student.StudentProfile?.ParentTrackingCode ?? string.Empty,
            "AssessmentName" => result.Title,
            "Score" => Number(result.Score),
            "TotalScore" => Number(result.Total),
            "Percentage" => result.Total > 0 ? $"{Number(result.Score * 100 / result.Total)}%" : "غير متاح",
            "Evaluation" => result.Evaluation,
            "LessonName" => lesson?.Title ?? result.Title,
            "SubjectName" => lesson?.Subject ?? result.Title,
            "TeacherName" => lesson?.Teacher ?? "المدرس",
            "Literal" => parameter.Literal!,
            _ => throw new InvalidOperationException("Unsupported assessment result parameter.")
        }).ToArray();
    }

    private static string Number(decimal number) => number.ToString("0.##", CultureInfo.InvariantCulture);
}
