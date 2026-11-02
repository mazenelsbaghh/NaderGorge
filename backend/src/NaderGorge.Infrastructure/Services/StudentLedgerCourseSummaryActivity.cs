using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities.Homework;
using System.Globalization;
using System.Text.Json;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class StudentLedgerExportService
{
    private async Task<SummaryActivity> LoadSummaryActivityAsync(Guid[] studentIds, List<SummaryLesson> lessons, CancellationToken ct)
    {
        var videos = lessons.SelectMany(lesson => lesson.Videos).ToArray();
        var videoIds = videos.Select(video => video.Id).ToArray();
        var started = await _db.VideoWatchEvents.AsNoTracking().Where(watch => studentIds.Contains(watch.UserId)
                && videoIds.Contains(watch.LessonVideoId) && (watch.LearningWatchedSeconds > 0 || watch.ActualWatchedSeconds > 0
                    || watch.TimeWatchedInSeconds > 0 || watch.WatchCount > 0))
            .Select(watch => new { watch.UserId, watch.LessonVideoId }).ToListAsync(ct);
        var sessions = await _db.VideoPlaybackSessions.AsNoTracking().Where(session => studentIds.Contains(session.UserId)
                && videoIds.Contains(session.LessonVideoId) && session.HasRegisteredView)
            .Select(session => new { session.UserId, session.LessonVideoId }).Distinct().ToListAsync(ct);
        var examIds = videos.Where(video => video.ExamId.HasValue).Select(video => video.ExamId!.Value)
            .Concat(lessons.Where(lesson => lesson.Lesson.ExamId.HasValue).Select(lesson => lesson.Lesson.ExamId!.Value)).Distinct().ToArray();
        var lessonIds = lessons.Select(lesson => lesson.Lesson.Id).ToArray();
        var homeworks = await _db.Homeworks.AsNoTracking().Where(homework => lessonIds.Contains(homework.LessonId)
                && (homework.IsActive && homework.Questions.Any(question => !question.IsRetired) || homework.Submissions.Any(submission => studentIds.Contains(submission.StudentId))))
            .OrderBy(homework => homework.Title).ThenBy(homework => homework.Id)
            .Select(homework => new HomeworkRow(homework.Id, homework.LessonId, homework.Title)).ToListAsync(ct);
        return new(started.Concat(sessions).Select(watch => (watch.UserId, watch.LessonVideoId)).ToHashSet(),
            await SummaryExamsAsync(studentIds, examIds, ct), homeworks,
            await SummaryHomeworkAsync(studentIds, homeworks, ct));
    }

    private async Task<SummaryGradeBook> SummaryExamsAsync(Guid[] studentIds, Guid[] examIds, CancellationToken ct)
    {
        var titles = await _db.Exams.AsNoTracking().Where(exam => examIds.Contains(exam.Id))
            .ToDictionaryAsync(exam => exam.Id, exam => exam.Title, ct);
        var attempts = await _db.StudentExamAttempts.AsNoTracking()
            .Where(attempt => studentIds.Contains(attempt.UserId) && examIds.Contains(attempt.ExamId))
            .OrderByDescending(attempt => attempt.StartedAt ?? attempt.CreatedAt).ThenByDescending(attempt => attempt.Id)
            .Select(attempt => new SummaryExamAttempt(attempt.UserId, attempt.ExamId, attempt.ScoreAchieved, attempt.Evaluation,
                attempt.DefinitionSnapshotJson, attempt.IsPassed, attempt.IsTimeExpired, attempt.Answers.Sum(answer => answer.PointsAwarded)))
            .ToListAsync(ct);
        return new("لم يمتحن", titles, attempts.GroupBy(attempt => (attempt.StudentId, attempt.ExamId))
            .ToDictionary(group => group.Key, group => SummaryExamGrade(group.First())));
    }

    private static string SummaryExamGrade(SummaryExamAttempt attempt)
    {
        if (string.IsNullOrWhiteSpace(attempt.Evaluation)) return "بدأ ولم يسلم";
        if (attempt.Evaluation == "قيد التصحيح") return "بانتظار التصحيح";
        try
        {
            var projection = AssessmentAttemptScaleNormalizer.Project(attempt.Snapshot, attempt.ExamId, attempt.Score,
                attempt.Passed, attempt.Expired, attempt.AwardedPoints);
            return SummaryScore(projection.ScoreAchieved, projection.Definition.TotalScore);
        }
        catch (Exception error) when (error is InvalidOperationException or JsonException) { return "تحتاج مراجعة يدوية"; }
    }

    private async Task<SummaryGradeBook> SummaryHomeworkAsync(Guid[] studentIds, List<HomeworkRow> homeworks, CancellationToken ct)
    {
        var ids = homeworks.Select(homework => homework.Id).ToArray();
        var submissions = await _db.HomeworkSubmissions.AsNoTracking().Where(submission => studentIds.Contains(submission.StudentId)
                && ids.Contains(submission.HomeworkId))
            .OrderByDescending(submission => submission.StartedAt).ThenByDescending(submission => submission.Id)
            .Select(submission => new SummaryHomeworkSubmission(submission.StudentId, submission.HomeworkId, submission.Status,
                submission.OverallScore, submission.TotalScoreSnapshot, submission.DefinitionSnapshotJson)).ToListAsync(ct);
        return new("لم يسلم", homeworks.ToDictionary(homework => homework.Id, homework => homework.Title),
            submissions.GroupBy(submission => (submission.StudentId, submission.HomeworkId))
                .ToDictionary(group => group.Key, group => SummaryHomeworkGrade(group.First())));
    }

    private static string SummaryHomeworkGrade(SummaryHomeworkSubmission submission)
    {
        if (submission.Status != SubmissionStatus.Graded)
            return submission.Status == SubmissionStatus.InProgress ? "بدأ ولم يسلم"
                : submission.Status == SubmissionStatus.Missed ? "لم يسلم" : "بانتظار التصحيح";
        try
        {
            var total = submission.Snapshot is null ? submission.Total
                : AssessmentDefinitionSnapshot.Read(submission.Snapshot, "homework", submission.HomeworkId).TotalScore;
            return total is > 0 ? SummaryScore(submission.Score, total.Value) : "تحتاج مراجعة يدوية";
        }
        catch (Exception error) when (error is InvalidOperationException or JsonException) { return "تحتاج مراجعة يدوية"; }
    }

    private static string SummaryScore(decimal score, decimal total) =>
        $"{score.ToString("0.##", CultureInfo.InvariantCulture)} / {total.ToString("0.##", CultureInfo.InvariantCulture)}";

    private sealed record SummaryExamAttempt(Guid StudentId, Guid ExamId, decimal Score, string? Evaluation,
        string? Snapshot, bool Passed, bool Expired, decimal AwardedPoints);
    private sealed record SummaryHomeworkSubmission(Guid StudentId, Guid HomeworkId, SubmissionStatus Status,
        decimal Score, decimal? Total, string? Snapshot);
}
