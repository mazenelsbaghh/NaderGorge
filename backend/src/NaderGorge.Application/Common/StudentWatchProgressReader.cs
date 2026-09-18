using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Application.Common;

public sealed record StudentVideoProgress(Guid VideoId, Guid LessonId, int? DurationSeconds, decimal WatchedSeconds)
{
    // Browser clocks and whole-second asset metadata can leave a fully played part just short.
    public bool IsCompleted => DurationSeconds is > 0
        && WatchedSeconds >= DurationSeconds.Value - Math.Min(2m, DurationSeconds.Value * 0.01m);
    public decimal CompletionWatchedSeconds => IsCompleted ? DurationSeconds!.Value : WatchedSeconds;
    public DateTime? LastWatchedAt { get; init; }
}

public static class StudentWatchProgressReader
{
    public static async Task<List<StudentVideoProgress>> ReadAsync(
        StudentLessonCompletionContext context,
        IReadOnlyCollection<Guid> visibleVideoIds,
        CancellationToken ct,
        IBunnyVideoDurationResolver? durationResolver = null)
    {
        if (visibleVideoIds.Count == 0) return [];
        var ids = visibleVideoIds.ToList();
        var videos = await context.Db.LessonVideos.AsNoTracking()
            .Where(video => ids.Contains(video.Id))
            .Select(video => new
            {
                video.Id,
                video.LessonId,
                video.Provider,
                video.ProviderVideoId,
                video.BunnyStreamLibraryId,
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
            .Select(watch => new { watch.LessonVideoId, watch.LearningWatchedSeconds, watch.LearningDurationSeconds, LastWatchedAt = watch.UpdatedAt ?? watch.CreatedAt })
            .ToDictionaryAsync(watch => watch.LessonVideoId, ct);
        var progress = videos.Select(video => new StudentVideoProgress(video.Id, video.LessonId, video.Duration is > 0 ? video.Duration
            : watches.GetValueOrDefault(video.Id)?.LearningDurationSeconds,
            Math.Max(0, watches.GetValueOrDefault(video.Id)?.LearningWatchedSeconds ?? 0))
            { LastWatchedAt = watches.GetValueOrDefault(video.Id)?.LastWatchedAt }).ToList();
        if (durationResolver is null) return progress;
        // Only lesson-detail callers hydrate missing provider metadata; large reports remain database-only.
        using var metadataBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        metadataBudget.CancelAfter(TimeSpan.FromSeconds(5));
        for (var index = 0; index < progress.Count; index++)
        {
            var video = videos[index];
            if (progress[index].DurationSeconds is > 0 || video.BunnyStreamLibraryId is not { } libraryId
                || VideoProviders.Normalize(video.Provider) != VideoProviders.Bunny) continue;
            try
            {
                var duration = await durationResolver.ResolveAsync(libraryId, video.ProviderVideoId, metadataBudget.Token);
                if (duration is > 0) progress[index] = progress[index] with { DurationSeconds = duration };
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Missing metadata stays unknown when the provider exceeds the page's budget.
                break;
            }
        }
        return progress;
    }

    public static int? CalculatePercent(IReadOnlyCollection<StudentVideoProgress> videos)
    {
        // Unknown durations must not be reported as zero progress; replays cannot
        // let one video compensate for an unwatched part of the lesson.
        if (videos.Count == 0 || videos.Any(video => video.DurationSeconds is null or <= 0)) return null;
        var duration = videos.Sum(video => (long)video.DurationSeconds!.Value);
        var watched = videos.Sum(video => Math.Clamp(video.CompletionWatchedSeconds, 0, video.DurationSeconds!.Value));
        return (int)Math.Floor(watched * 100m / duration);
    }
}
