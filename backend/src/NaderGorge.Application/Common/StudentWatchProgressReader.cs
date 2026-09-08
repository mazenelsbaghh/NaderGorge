using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Common;

public sealed record StudentVideoProgress(Guid VideoId, Guid LessonId, int? DurationSeconds, decimal WatchedSeconds)
{
    public bool IsCompleted => DurationSeconds is > 0 && WatchedSeconds >= DurationSeconds.Value;
    public DateTime? LastWatchedAt { get; init; }
}

public static class StudentWatchProgressReader
{
    public static async Task<List<StudentVideoProgress>> ReadAsync(
        StudentLessonCompletionContext context,
        IReadOnlyCollection<Guid> visibleVideoIds,
        CancellationToken ct)
    {
        if (visibleVideoIds.Count == 0) return [];
        var ids = visibleVideoIds.ToList();
        var videos = await context.Db.LessonVideos.AsNoTracking()
            .Where(video => ids.Contains(video.Id))
            .Select(video => new
            {
                video.Id,
                video.LessonId,
                Duration = context.Db.BunnyVideoAssets
                    .Where(asset => asset.LessonVideoId == video.Id
                        && asset.SourceState == BunnyVideoAssetSourceState.Current && asset.DurationSeconds > 0)
                    .Select(asset => asset.DurationSeconds).FirstOrDefault()
                    ?? context.Db.VideoPlaybackSessions
                    .Where(session => session.UserId == context.UserId && session.LessonVideoId == video.Id)
                    .Max(session => session.TrackingDurationSeconds)
            }).ToListAsync(ct);
        var watches = await context.Db.VideoWatchEvents.AsNoTracking()
            .Where(watch => watch.UserId == context.UserId && ids.Contains(watch.LessonVideoId))
            .Select(watch => new { watch.LessonVideoId, watch.LearningWatchedSeconds, LastWatchedAt = watch.UpdatedAt ?? watch.CreatedAt })
            .ToDictionaryAsync(watch => watch.LessonVideoId, ct);
        return videos.Select(video => new StudentVideoProgress(video.Id, video.LessonId, video.Duration,
            Math.Max(0, watches.GetValueOrDefault(video.Id)?.LearningWatchedSeconds ?? 0))
            { LastWatchedAt = watches.GetValueOrDefault(video.Id)?.LastWatchedAt }).ToList();
    }

    public static int? CalculatePercent(IReadOnlyCollection<StudentVideoProgress> videos)
    {
        // Unknown durations must not be reported as zero progress; replays cannot
        // let one video compensate for an unwatched part of the lesson.
        if (videos.Count == 0 || videos.Any(video => video.DurationSeconds is null or <= 0)) return null;
        var duration = videos.Sum(video => (long)video.DurationSeconds!.Value);
        var watched = videos.Sum(video => Math.Clamp(video.WatchedSeconds, 0, video.DurationSeconds!.Value));
        return (int)Math.Floor(watched * 100m / duration);
    }
}
