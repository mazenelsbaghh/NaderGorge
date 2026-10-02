using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.LiveSupport;
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
        var kind = notification.Type is "ExamGraded" or "AssessmentParentRecovery" or "AssessmentParentRetry" ? "exam" : "homework";
        Guid attemptId;
        if (notification.Type == "AssessmentParentRecovery")
        {
            var envelope = JsonSerializer.Deserialize<AssessmentParentRecoveryEnvelope>(notification.PayloadJson);
            if (envelope is null || envelope.AttemptId == Guid.Empty || envelope.DeliveryId == Guid.Empty
                || envelope.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.GradeVersion)) return null;
            attemptId = envelope.AttemptId;
        }
        else if (!payload.RootElement.TryGetProperty(kind == "exam" ? "attemptId" : "submissionId", out var id)
            || !id.TryGetGuid(out attemptId)) return null;
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
            .Include(attempt => attempt.Answers)
            .Include(attempt => attempt.User).ThenInclude(student => student.StudentProfile)
            .SingleOrDefaultAsync(attempt => attempt.Id == attemptId, ct);
        if (attempt is null || string.IsNullOrWhiteSpace(attempt.Evaluation) || attempt.Evaluation == "قيد التصحيح") return null;
        if (attempt.DefinitionSnapshotJson is null) return null;
        var scale = AssessmentAttemptScaleNormalizer.Project(attempt);
        var definition = scale.Definition;
        if (definition.Revision is { } revision && (revision.RequiresCompletion || revision.RequiresReview)) return null;
        var assignedQuestionIds = definition.Questions.Select(question => question.BankQuestionId).ToArray();
        if (await db.EssaySubmissions.AnyAsync(essay => essay.StudentExamAttemptId == attemptId
            && assignedQuestionIds.Contains(essay.QuestionId)
            && essay.Status != EssaySubmissionStatus.TeacherGraded, ct)) return null;
        var lessonId = await db.Lessons.Where(lesson => lesson.ExamId == attempt.ExamId)
            .Select(lesson => (Guid?)lesson.Id).FirstOrDefaultAsync(ct)
            ?? await db.LessonVideos.Where(video => video.ExamId == attempt.ExamId)
                .Select(video => (Guid?)video.LessonId).FirstOrDefaultAsync(ct);
        return new("exam", attempt.ExamId, attempt.Id, attempt.User,
            AssessmentParentNotificationSettings.Read(attempt.Exam.ParentNotificationSettingsJson),
            attempt.Exam.ParentNotificationEnabledAt, definition.Title,
            scale.ScoreAchieved, definition.TotalScore, attempt.Evaluation, lessonId);
    }

    public static async Task<string[]> ParametersAsync(IAppDbContext db, AssessmentParentResult result, CancellationToken ct,
        LiveSupportWhatsAppTemplate? template = null)
    {
        var lesson = await db.Lessons.AsNoTracking().Where(lesson => lesson.Id == result.LessonId)
            .Select(lesson => new { lesson.Title, Subject = lesson.ContentSection.Term.Package.Subject.Name,
                Teacher = lesson.ContentSection.Term.Package.Teacher.User.FullName }).SingleOrDefaultAsync(ct);
        var parameters = result.Settings.Parameters.Select(parameter => parameter.Source switch
        {
            "ParentName" => $"ولي أمر {AssessmentResultNames.StudentName(result.Student.FullName)}",
            "StudentName" => AssessmentResultNames.StudentName(result.Student.FullName),
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
        if (template is not null) FitHeaderNames(template, result.Settings.Parameters, parameters);
        return parameters;
    }

    private static void FitHeaderNames(LiveSupportWhatsAppTemplate template,
        AssessmentResultParameter[] sources, string[] parameters)
    {
        using var components = JsonDocument.Parse(template.ComponentsJson);
        var offset = 0;
        foreach (var component in components.RootElement.EnumerateArray())
        {
            var type = component.GetProperty("type").GetString();
            if (type is not ("HEADER" or "BODY") || !component.TryGetProperty("text", out var text)) continue;
            var content = text.GetString()!;
            var placeholders = Regex.Matches(content, @"\{\{\d+\}\}");
            var count = placeholders.Select(match => match.Value).Distinct().Count();
            if (type == "HEADER" && placeholders.Count == 1 && placeholders[0].Value == "{{1}}"
                && offset < parameters.Length && sources[offset].Source is "StudentName" or "ParentName")
            {
                var budget = 60 - (content.Length - "{{1}}".Length);
                if (budget > 0 && parameters[offset].Length > budget)
                {
                    var name = parameters[offset];
                    var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    while (words.Length > 1 && string.Join(' ', words).Length > budget)
                        words = words[..^1];
                    var shortName = string.Join(' ', words);
                    if (shortName.Length > budget)
                    {
                        var end = StringInfo.ParseCombiningCharacters(shortName)
                            .LastOrDefault(index => index <= budget);
                        shortName = shortName[..end];
                    }
                    if (shortName.Length > 0) parameters[offset] = shortName;
                }
            }
            offset += count;
        }
    }

    private static string Number(decimal number) => number.ToString("0.##", CultureInfo.InvariantCulture);
}
