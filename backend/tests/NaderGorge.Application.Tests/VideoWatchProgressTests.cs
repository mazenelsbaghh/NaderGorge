using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Tracking.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class VideoWatchProgressTests
{
    [Fact]
    public async Task RecordVideoEvent_DoesNotCountPastMaxLimit_WhenFlushCrossesMultipleThresholds()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var video = await SeedVideoAsync(db, maxWatchCount: 3);

        var handler = new RecordVideoEventCommandHandler(db, FixedSettingsReader.Default);
        var result = await handler.Handle(
            new RecordVideoEventCommand(userId, video.Id, WatchedSeconds: 30, TotalDurationSeconds: 10),
            CancellationToken.None);

        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(30, watchEvent.TimeWatchedInSeconds);
        Assert.Equal(3, watchEvent.WatchCount);
        Assert.True(watchEvent.IsLocked);
        Assert.Equal(3, result.Data!.WatchCount);
        Assert.Equal(0, result.Data.RemainingSecondsForNextWatch);
    }

    [Fact]
    public async Task TrackWatchProgress_CountsDeltasOnce_AndLocksAtMaxLimit()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var video = await SeedVideoAsync(db, maxWatchCount: 2);

        var handler = new TrackWatchProgressCommandHandler(db, FixedSettingsReader.Default);
        var firstResult = await handler.Handle(
            new TrackWatchProgressCommand(video.Id, userId, SecondsWatched: 30, TotalDurationSeconds: 100),
            CancellationToken.None);

        Assert.True(firstResult.Success);
        Assert.NotNull(firstResult.Data);
        Assert.Equal(1, firstResult.Data!.CurrentCount);
        Assert.True(firstResult.Data.ViewRegistered);
        Assert.False(firstResult.Data.IsLocked);
        Assert.Equal(30, firstResult.Data.TotalTrackedSeconds);

        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        // Simulate elapsed wall-clock time; SaveChanges would overwrite UpdatedAt before the handler reads it.
        watchEvent.UpdatedAt = DateTime.UtcNow.AddSeconds(-31);

        var secondResult = await handler.Handle(
            new TrackWatchProgressCommand(video.Id, userId, SecondsWatched: 30, TotalDurationSeconds: 100),
            CancellationToken.None);

        Assert.True(secondResult.Success);
        Assert.NotNull(secondResult.Data);
        Assert.Equal(2, secondResult.Data!.CurrentCount);
        Assert.True(secondResult.Data.ViewRegistered);
        Assert.True(secondResult.Data.IsLocked);
        Assert.Equal(60, secondResult.Data.TotalTrackedSeconds);

        var lockedTotal = secondResult.Data.TotalTrackedSeconds;

        var lockedResult = await handler.Handle(
            new TrackWatchProgressCommand(video.Id, userId, SecondsWatched: 30, TotalDurationSeconds: 100),
            CancellationToken.None);

        Assert.True(lockedResult.Success);
        Assert.NotNull(lockedResult.Data);
        Assert.Equal(2, lockedResult.Data!.CurrentCount);
        Assert.False(lockedResult.Data.ViewRegistered);
        Assert.True(lockedResult.Data.IsLocked);
        Assert.Equal(lockedTotal, lockedResult.Data.TotalTrackedSeconds);
    }

    [Fact]
    public async Task RecordVideoEvent_ReturnsWatchLimitErrorContext_WhenAlreadyLocked()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var video = await SeedVideoAsync(db, maxWatchCount: 1);
        db.VideoWatchEvents.Add(new VideoWatchEvent
        {
            UserId = userId,
            LessonVideoId = video.Id,
            WatchCount = 1,
            TimeWatchedInSeconds = 30,
            IsLocked = true
        });
        await db.SaveChangesAsync();

        var handler = new RecordVideoEventCommandHandler(db, FixedSettingsReader.Default);
        var result = await handler.Handle(
            new RecordVideoEventCommand(userId, video.Id, WatchedSeconds: 10, TotalDurationSeconds: 100),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("WATCH_LIMIT_REACHED", result.Errors!);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.WatchCount);
        Assert.True(result.Data.IsLocked);
        Assert.Equal(0, result.Data.RemainingSecondsForNextWatch);
    }

    private static async Task<LessonVideo> SeedVideoAsync(AppDbContext db, int maxWatchCount)
    {
        var video = new LessonVideo
        {
            Id = Guid.NewGuid(),
            LessonId = Guid.NewGuid(),
            Title = "Test video",
            Provider = "youtube",
            ProviderVideoId = "video-id",
            MaxWatchCount = maxWatchCount
        };

        db.LessonVideos.Add(video);
        await db.SaveChangesAsync();
        return video;
    }

    private sealed class FixedSettingsReader : ICachedPlatformSettingsReader
    {
        public static readonly FixedSettingsReader Default = new(CachedPlatformSettings.Default with
        {
            VideoWatchThresholdPercentage = 30
        });

        private readonly CachedPlatformSettings _settings;

        private FixedSettingsReader(CachedPlatformSettings settings)
        {
            _settings = settings;
        }

        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_settings);
        }

        public void Invalidate()
        {
        }
    }
}
