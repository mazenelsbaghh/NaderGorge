using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Tracking.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class VideoWatchProgressTests
{
    [Fact]
    public async Task TrackWatchProgress_RegistersAtMostOneViewPerSession_AndDiscardsExcess()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);

        var first = await handler.Handle(Command(fixture, sequence: 1, seconds: 40), CancellationToken.None);
        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        var session = await db.VideoPlaybackSessions.SingleAsync();

        Assert.True(first.Success);
        Assert.True(first.Data!.ViewRegistered);
        Assert.Equal(1, first.Data.CurrentCount);
        Assert.Equal(30, first.Data.TotalTrackedSeconds);
        Assert.True(session.HasRegisteredView);

        watchEvent.UpdatedAt = DateTime.UtcNow.AddMinutes(-1);
        var second = await handler.Handle(Command(fixture, sequence: 2, seconds: 30), CancellationToken.None);

        Assert.True(second.Success);
        Assert.False(second.Data!.ViewRegistered);
        Assert.Equal(1, second.Data.CurrentCount);
        Assert.Equal(30, second.Data.TotalTrackedSeconds);
    }

    [Fact]
    public async Task TrackWatchProgress_RepeatedSequence_IsIdempotent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);

        var first = await handler.Handle(Command(fixture, sequence: 1, seconds: 10), CancellationToken.None);
        var repeated = await handler.Handle(Command(fixture, sequence: 1, seconds: 10), CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(repeated.Success);
        Assert.True(repeated.Data!.Duplicate);
        Assert.Equal(10, repeated.Data.TotalTrackedSeconds);
        Assert.Equal(0, repeated.Data.CurrentCount);
    }

    [Fact]
    public async Task TrackWatchProgress_AccumulatesIncompleteTimeAcrossSessions()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);

        await handler.Handle(Command(fixture, sequence: 1, seconds: 3), CancellationToken.None);
        var firstSession = await db.VideoPlaybackSessions.SingleAsync();
        firstSession.IsSuperseded = true;
        var secondSession = NewSession(fixture.UserId, fixture.Video.Id);
        db.VideoPlaybackSessions.Add(secondSession);
        await db.SaveChangesAsync();

        var second = await handler.Handle(
            new TrackWatchProgressCommand(fixture.Video.Id, fixture.UserId, secondSession.Id, 1, 3, 100),
            CancellationToken.None);

        Assert.True(second.Success);
        Assert.Equal(6, second.Data!.TotalTrackedSeconds);
        Assert.Equal(0, second.Data.CurrentCount);
    }

    [Fact]
    public async Task TrackWatchProgress_RejectsSupersededSessionWithoutMutation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var oldSession = await db.VideoPlaybackSessions.SingleAsync();
        oldSession.IsSuperseded = true;
        db.VideoPlaybackSessions.Add(NewSession(fixture.UserId, fixture.Video.Id));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(Command(fixture, 1, 10), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("SESSION_SUPERSEDED", result.Errors!);
        Assert.Empty(db.VideoWatchEvents);
    }

    [Fact]
    public async Task TrackWatchProgress_RejectsExpiredSessionWithoutMutation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        session.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();

        var expired = await CreateHandler(db).Handle(Command(fixture, 1, 10), CancellationToken.None);

        Assert.False(expired.Success);
        Assert.Contains("SESSION_EXPIRED", expired.Errors!);
        Assert.Empty(db.VideoWatchEvents);
    }

    [Fact]
    public async Task TrackWatchProgress_RejectsMismatchedSessionOwnerWithoutMutation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);

        var mismatched = await CreateHandler(db).Handle(
            new TrackWatchProgressCommand(fixture.Video.Id, Guid.NewGuid(), fixture.SessionId, 1, 10, 100),
            CancellationToken.None);

        Assert.False(mismatched.Success);
        Assert.Contains("SESSION_INVALID", mismatched.Errors!);
        Assert.Empty(db.VideoWatchEvents);
    }

    [Fact]
    public async Task TrackWatchProgress_RenewsSameSessionWithoutResettingRegisteredView()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);

        await handler.Handle(Command(fixture, 1, 30), CancellationToken.None);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        var firstExpiry = session.ExpiresAt;
        session.ExpiresAt = DateTime.UtcNow.AddSeconds(1);
        await db.SaveChangesAsync();

        var heartbeat = await handler.Handle(Command(fixture, 2, 10), CancellationToken.None);

        Assert.True(heartbeat.Success);
        Assert.True(session.HasRegisteredView);
        Assert.True(session.ExpiresAt > firstExpiry);
        Assert.Equal(30, heartbeat.Data!.TotalTrackedSeconds);
    }

    [Fact]
    public async Task TrackWatchProgress_LocksExactlyAtCustomMaximum()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 5);
        db.VideoWatchEvents.Add(new VideoWatchEvent
        {
            UserId = fixture.UserId,
            LessonVideoId = fixture.Video.Id,
            WatchCount = 1,
            TimeWatchedInSeconds = 30,
            CustomMaxWatchCount = 2
        });
        await db.SaveChangesAsync();
        var existingWatchEvent = await db.VideoWatchEvents.SingleAsync();
        existingWatchEvent.UpdatedAt = DateTime.UtcNow.AddSeconds(-31);

        var result = await CreateHandler(db).Handle(Command(fixture, 1, 30), CancellationToken.None);

        Assert.True(result.Success, $"{result.Message}: {string.Join(",", result.Errors ?? [])}");
        Assert.Equal(2, result.Data!.CurrentCount);
        Assert.True(result.Data.IsLocked);
        Assert.Equal(60, result.Data.TotalTrackedSeconds);
    }

    [Fact]
    public async Task RecordVideoEvent_RequiresPlaybackSession_AndDoesNotMutateState()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = new RecordVideoEventCommandHandler();

        var result = await handler.Handle(
            new RecordVideoEventCommand(fixture.UserId, fixture.Video.Id, WatchedSeconds: 30, TotalDurationSeconds: 100),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("SESSION_REQUIRED", result.Errors!);
        Assert.Empty(db.VideoWatchEvents);
    }

    [Fact]
    public async Task CreateVideoSession_AlwaysCreatesNewestSession_AndSupersedesPriorSession()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = new CreateVideoSessionCommandHandler(db, AllowAccess.Instance, FakeEncryption.Instance);

        var result = await handler.Handle(
            new CreateVideoSessionCommand(fixture.Video.Id, fixture.UserId),
            CancellationToken.None);

        var sessions = await db.VideoPlaybackSessions.OrderBy(s => s.CreatedAt).ToListAsync();
        Assert.True(result.Success, $"{result.Message}: {string.Join(",", result.Errors ?? [])}");
        Assert.Equal(2, sessions.Count);
        Assert.True(sessions[0].IsSuperseded);
        Assert.False(sessions[1].IsSuperseded);
        Assert.NotEqual(fixture.SessionId, result.Data!.SessionId);
    }

    private static TrackWatchProgressCommandHandler CreateHandler(AppDbContext db) =>
        new(db, FixedSettingsReader.Default);

    private static TrackWatchProgressCommand Command(Fixture fixture, long sequence, double seconds) =>
        new(fixture.Video.Id, fixture.UserId, fixture.SessionId, sequence, seconds, 100);

    private static async Task<Fixture> SeedFixtureAsync(AppDbContext db, int maxWatchCount)
    {
        var userId = Guid.NewGuid();
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            ContentSectionId = Guid.NewGuid(),
            Title = "Test lesson"
        };
        var video = new LessonVideo
        {
            Id = Guid.NewGuid(),
            LessonId = lesson.Id,
            Lesson = lesson,
            Title = "Test video",
            Provider = "youtube",
            ProviderVideoId = "video-id",
            MaxWatchCount = maxWatchCount
        };
        var session = NewSession(userId, video.Id);

        db.Lessons.Add(lesson);
        db.LessonVideos.Add(video);
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();
        return new Fixture(userId, video, session.Id);
    }

    private static VideoPlaybackSession NewSession(Guid userId, Guid videoId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        LessonVideoId = videoId,
        SessionToken = "token",
        EncryptionKey = "key",
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
    };

    private sealed record Fixture(Guid UserId, LessonVideo Video, Guid SessionId);

    private sealed class FixedSettingsReader : ICachedPlatformSettingsReader
    {
        public static readonly FixedSettingsReader Default = new();

        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { VideoWatchThresholdPercentage = 30 });

        public void Invalidate()
        {
        }
    }

    private sealed class AllowAccess : IAccessCheckService
    {
        public static readonly AllowAccess Instance = new();
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeEncryption : IVideoEncryptionService
    {
        public static readonly FakeEncryption Instance = new();
        public string EncryptVideoInfo(string providerName, string providerVideoId, string sessionKey, string? studentName = null, string? studentPhone = null) => "encrypted";
        public (string ProviderName, string ProviderVideoId, string? StudentName, string? StudentPhone) DecryptVideoInfo(string encryptedToken, string sessionKey) => ("youtube", "video-id", null, null);
        public string GenerateSessionKey() => "session-key";
    }
}
