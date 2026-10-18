using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.StudentFollowUp;

public sealed partial class StudentFollowUpReader
{
    private async Task<List<StudentFollowUpRow>> BuildRowsAsync(StudentFollowUpFilter filter,
        List<VideoRow> videos, List<GrantRow> grants, CancellationToken ct)
    {
        if (grants.Count == 0) return [];
        var videoIds = videos.Select(v => v.Id).ToArray();
        var studentIds = grants.Select(g => g.UserId).Distinct().ToArray();
        var watches = await db.VideoWatchEvents.AsNoTracking()
            .Where(w => studentIds.Contains(w.UserId) && videoIds.Contains(w.LessonVideoId))
            .Select(w => new WatchRow(w.UserId, w.LessonVideoId, w.LearningWatchedSeconds,
                w.LearningDurationSeconds, w.ActualWatchedSeconds > 0 || w.TimeWatchedInSeconds > 0 || w.WatchCount > 0,
                w.UpdatedAt ?? w.CreatedAt)).ToDictionaryAsync(w => (w.UserId, w.VideoId), ct);
        var sessions = await db.VideoPlaybackSessions.AsNoTracking()
            .Where(s => studentIds.Contains(s.UserId) && videoIds.Contains(s.LessonVideoId))
            .GroupBy(s => new { s.UserId, s.LessonVideoId })
            .Select(g => new { g.Key.UserId, g.Key.LessonVideoId, Duration = g.Max(s => s.TrackingDurationSeconds),
                Started = g.Any(s => s.HasRegisteredView),
                LastPlayedAt = g.Max(s => s.HasRegisteredView ? s.LastProgressAt ?? (DateTime?)s.CreatedAt : null) })
            .ToDictionaryAsync(s => (s.UserId, s.LessonVideoId), ct);
        var eligibility = new Dictionary<(EducationStage, GradeLevel), IReadOnlySet<Guid>>();
        foreach (var cohort in grants.GroupBy(g => (g.Stage, g.Grade)))
        {
            // Academic eligibility is shared by stage/grade; access grants stay individual below.
            eligibility[cohort.Key] = await academicScope.GetEligibleLessonVideoIdsForStudentAsync(
                videoIds, cohort.First().UserId, ct);
        }
        var rows = new List<StudentFollowUpRow>();
        foreach (var studentGrants in grants.GroupBy(g => g.UserId))
        {
            var identity = studentGrants.First();
            var parts = new List<PartProgress>();
            foreach (var video in videos.Where(v => eligibility[(identity.Stage, identity.Grade)].Contains(v.Id)))
            {
                var matching = studentGrants.Where(g => GrantsVideo(g, video, filter.TeacherId)).ToArray();
                if (matching.Length == 0) continue;
                watches.TryGetValue((identity.UserId, video.Id), out var watch);
                var session = sessions.GetValueOrDefault((identity.UserId, video.Id));
                var duration = video.Duration ?? session?.Duration ?? watch?.Duration;
                var progress = new StudentVideoProgress(video.Id, video.LessonId, duration, Math.Max(0, watch?.Seconds ?? 0));
                var openedAt = video.OpenedAt ?? video.FirstPlaybackAt;
                DateTime? availableAt = openedAt is null ? null : new[] { openedAt.Value, matching.Min(g => g.GrantedAt) }.Max();
                parts.Add(new(video, progress, availableAt, video.OpenedAt is null ? "RecordedPlayback" : "VideoOpened",
                    progress.WatchedSeconds > 0 || watch?.HasLegacyActivity == true || session?.Started == true,
                    new[] { watch?.LastWatchedAt, session?.LastPlayedAt }.Max()));
            }
            rows.AddRange(parts.GroupBy(p => p.Video.LessonId).Select(lesson => LessonRow(identity, lesson.ToArray())));
        }
        return rows;
    }

    private StudentFollowUpRow LessonRow(GrantRow student, PartProgress[] parts)
    {
        var completed = parts.All(p => p.Progress.IsCompleted);
        var state = completed ? "Completed" : parts.Any(p => p.Started) ? "Started" : "NotStarted";
        var waiting = (completed ? parts : parts.Where(p => !p.Progress.IsCompleted)).ToArray();
        var oldest = waiting.Where(p => p.AvailableAt.HasValue).OrderBy(p => p.AvailableAt).FirstOrDefault();
        var days = oldest?.AvailableAt is { } availableAt
            ? Math.Max(0, (int)Math.Floor((time.GetUtcNow().UtcDateTime - availableAt).TotalDays)) : (int?)null;
        var video = parts[0].Video;
        return new(student.UserId, student.Name, student.Phone, student.ParentPhone, student.Stage.ToString(), student.Grade.ToString(),
            video.LessonId, video.LessonTitle, video.PackageName, state, parts.Count(p => p.Progress.IsCompleted), parts.Length,
            StudentWatchProgressReader.CalculatePercent(parts.Select(p => p.Progress).ToArray()), oldest?.AvailableAt,
            completed ? 0 : days, oldest?.Source ?? "Unknown", parts.Where(p => p.Started).Max(p => p.LastWatchedAt));
    }

    private static bool GrantsVideo(GrantRow grant, VideoRow video, Guid teacherId) => grant.Type switch
    {
        CodeType.Package => grant.PackageId == video.PackageId,
        CodeType.Term => grant.TermId == video.TermId,
        CodeType.Month => grant.SectionId == video.SectionId,
        CodeType.Lesson => grant.LessonId == video.LessonId,
        CodeType.Video => grant.VideoId == video.Id || (grant.VideoTypeId == video.TypeId
            && (grant.LessonId is null || grant.LessonId == video.LessonId)
            && (grant.SectionId is null || grant.SectionId == video.SectionId)
            && (grant.TermId is null || grant.TermId == video.TermId)
            && (grant.PackageId is null || grant.PackageId == video.PackageId)
            && (grant.CodeTeacherId is null || grant.CodeTeacherId == teacherId)),
        _ => false
    };

    private sealed record WatchRow(Guid UserId, Guid VideoId, decimal Seconds, int? Duration, bool HasLegacyActivity, DateTime LastWatchedAt);
    private sealed record PartProgress(VideoRow Video, StudentVideoProgress Progress, DateTime? AvailableAt,
        string Source, bool Started, DateTime? LastWatchedAt);
}
