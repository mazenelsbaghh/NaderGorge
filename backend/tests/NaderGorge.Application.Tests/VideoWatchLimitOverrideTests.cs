using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class VideoWatchLimitOverrideTests
{
    [Fact]
    public async Task LockedStudentGivenOneExtraView_KeepsUsageAndRaisesLimit()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedWatchEventAsync(db, maxWatchCount: 4, watchCount: 4);
        db.ChangeTracker.Clear();
        var handler = new OverrideVideoLimitCommandHandler(db);

        var response = await handler.Handle(
            new OverrideVideoLimitCommand(fixture.StudentId, fixture.VideoId, 1, "فتح مشاهدة إضافية", fixture.AdminId),
            default);

        Assert.True(response.Success);
        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        Assert.Equal(4, watchEvent.WatchCount);
        Assert.Equal(5, watchEvent.CustomMaxWatchCount);
        Assert.False(watchEvent.IsLocked);

        var videoOverride = await db.VideoOverrides.SingleAsync();
        Assert.Equal(4, videoOverride.OriginalLimit);
        Assert.Equal(5, videoOverride.NewLimit);
        Assert.Equal(1, videoOverride.AddedViews);
        Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Single(await db.OutboxEvents.Where(entry => entry.Type == "VideoWatchLimitChanged").ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveExtraViews_AreRejectedWithoutChangingWatchState(int addedViews)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedWatchEventAsync(db, maxWatchCount: 4, watchCount: 4);
        var handler = new OverrideVideoLimitCommandHandler(db);

        var response = await handler.Handle(
            new OverrideVideoLimitCommand(fixture.StudentId, fixture.VideoId, addedViews, "غير صالح", fixture.AdminId),
            default);

        Assert.False(response.Success);
        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        Assert.Equal(4, watchEvent.WatchCount);
        Assert.Null(watchEvent.CustomMaxWatchCount);
        Assert.True(watchEvent.IsLocked);
        Assert.Empty(await db.VideoOverrides.ToListAsync());
    }

    [Fact]
    public async Task UnlimitedVideo_RejectsExtraViewsWithoutCreatingOverride()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedWatchEventAsync(db, maxWatchCount: 0, watchCount: 0);
        var handler = new OverrideVideoLimitCommandHandler(db);

        var response = await handler.Handle(
            new OverrideVideoLimitCommand(fixture.StudentId, fixture.VideoId, 1, "غير مطلوب", fixture.AdminId),
            default);

        Assert.False(response.Success);
        Assert.Empty(await db.VideoOverrides.ToListAsync());
        Assert.Null((await db.VideoWatchEvents.SingleAsync()).CustomMaxWatchCount);
    }

    private static async Task<Fixture> SeedWatchEventAsync(
        AppDbContext db,
        int maxWatchCount,
        int watchCount)
    {
        var studentId = Guid.NewGuid();
        var video = NewVideo(maxWatchCount);
        db.LessonVideos.Add(video);
        db.VideoWatchEvents.Add(NewWatchEvent(studentId, video.Id, watchCount, maxWatchCount));
        await db.SaveChangesAsync();
        return new Fixture(studentId, video.Id, Guid.NewGuid());
    }

    private static LessonVideo NewVideo(int maxWatchCount) => new()
    {
        Id = Guid.NewGuid(),
        LessonId = Guid.NewGuid(),
        Title = "فيديو تجريبي",
        Provider = "youtube",
        ProviderVideoId = "video-id",
        MaxWatchCount = maxWatchCount
    };

    private static VideoWatchEvent NewWatchEvent(Guid studentId, Guid videoId, int watchCount, int maxWatchCount) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = studentId,
            LessonVideoId = videoId,
            WatchCount = watchCount,
            IsLocked = maxWatchCount > 0 && watchCount >= maxWatchCount
        };

    private sealed record Fixture(Guid StudentId, Guid VideoId, Guid AdminId);
}
