using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Tracking.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
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
        Assert.True(second.Data.SessionHasRegisteredView);
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
    public async Task TrackWatchProgress_DuplicateAfterLostResponse_ReportsRegisteredSessionView()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);

        var committed = await handler.Handle(Command(fixture, sequence: 1, seconds: 30), CancellationToken.None);
        var retry = await handler.Handle(Command(fixture, sequence: 1, seconds: 30), CancellationToken.None);

        Assert.True(committed.Success);
        Assert.True(committed.Data!.ViewRegistered);
        Assert.True(retry.Success);
        Assert.True(retry.Data!.Duplicate);
        Assert.False(retry.Data.ViewRegistered);
        Assert.True(retry.Data.SessionHasRegisteredView);
        Assert.Equal(1, retry.Data.CurrentCount);
        Assert.Equal(30, retry.Data.TotalTrackedSeconds);
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
            new TrackWatchProgressCommand(fixture.Video.Id, fixture.UserId, secondSession.Id, 1, 3, 1, 100),
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
            new TrackWatchProgressCommand(fixture.Video.Id, Guid.NewGuid(), fixture.SessionId, 1, 10, 1, 100),
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
    public async Task TrackWatchProgress_RenewsSessionForLongVideoPlaybackWindow()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        session.ExpiresAt = DateTime.UtcNow.AddMinutes(1);
        await db.SaveChangesAsync();
        var beforeTracking = DateTime.UtcNow;

        var heartbeat = await CreateHandler(db).Handle(
            new TrackWatchProgressCommand(
                fixture.Video.Id,
                fixture.UserId,
                fixture.SessionId,
                ProgressSequence: 1,
                SecondsWatched: 10,
                PlaybackRate: 1,
                TotalDurationSeconds: 4 * 60 * 60),
            CancellationToken.None);

        Assert.True(heartbeat.Success);
        Assert.True(session.ExpiresAt >= beforeTracking.AddHours(4.5));
    }

    [Fact]
    public async Task TrackWatchProgress_UsesAuthoritativeBunnyDuration_InsteadOfClientDuration()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        fixture.Video.Provider = VideoProviders.Bunny;
        db.BunnyVideoAssets.Add(new BunnyVideoAsset
        {
            Id = Guid.NewGuid(),
            LessonVideoId = fixture.Video.Id,
            TeacherId = Guid.NewGuid(),
            PackageId = Guid.NewGuid(),
            LessonId = fixture.Video.LessonId,
            UploadedByUserId = Guid.NewGuid(),
            BunnyLibraryId = 123,
            BunnyVideoGuid = "official-video-guid",
            Title = "Official Bunny asset",
            UploadMethod = "DirectUpload",
            Status = "Ready",
            SourceState = BunnyVideoAssetSourceState.Current,
            DurationSeconds = 200,
        });
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new TrackWatchProgressCommand(
                fixture.Video.Id,
                fixture.UserId,
                fixture.SessionId,
                ProgressSequence: 1,
                SecondsWatched: 30,
                PlaybackRate: 1,
                TotalDurationSeconds: 100),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(60, result.Data!.ThresholdSeconds);
        Assert.False(result.Data.ViewRegistered);
        Assert.Equal(0, result.Data.CurrentCount);
        Assert.Equal(30, result.Data.TotalTrackedSeconds);
    }

    [Fact]
    public async Task TrackWatchProgress_SnapshotsBunnyDurationAndThresholdForSession()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        fixture.Video.Provider = VideoProviders.Bunny;
        var asset = new BunnyVideoAsset
        {
            Id = Guid.NewGuid(),
            LessonVideoId = fixture.Video.Id,
            TeacherId = Guid.NewGuid(),
            PackageId = Guid.NewGuid(),
            LessonId = fixture.Video.LessonId,
            UploadedByUserId = Guid.NewGuid(),
            BunnyLibraryId = 123,
            BunnyVideoGuid = "immutable-video-guid",
            Title = "Immutable Bunny asset",
            UploadMethod = "DirectUpload",
            Status = "Ready",
            SourceState = BunnyVideoAssetSourceState.Current,
            DurationSeconds = 200
        };
        var thresholdSetting = new PlatformSetting
        {
            Key = PlatformSettingKeys.BunnyWatchThresholdPercentage,
            Value = "30"
        };
        db.AddRange(asset, thresholdSetting);
        await db.SaveChangesAsync();
        var handler = CreateHandler(db);

        var first = await handler.Handle(
            new TrackWatchProgressCommand(
                fixture.Video.Id,
                fixture.UserId,
                fixture.SessionId,
                ProgressSequence: 1,
                SecondsWatched: 10,
                PlaybackRate: 1,
                TotalDurationSeconds: 1),
            CancellationToken.None);

        asset.DurationSeconds = 400;
        thresholdSetting.Value = "50";
        await db.SaveChangesAsync();
        var second = await handler.Handle(
            new TrackWatchProgressCommand(
                fixture.Video.Id,
                fixture.UserId,
                fixture.SessionId,
                ProgressSequence: 2,
                SecondsWatched: 10,
                PlaybackRate: 1,
                TotalDurationSeconds: 0),
            CancellationToken.None);
        var session = await db.VideoPlaybackSessions.SingleAsync();

        Assert.True(first.Success, first.Message);
        Assert.True(second.Success, second.Message);
        Assert.Equal(60, first.Data!.ThresholdSeconds);
        Assert.Equal(60, second.Data!.ThresholdSeconds);
        Assert.Equal(200, session.TrackingDurationSeconds);
        Assert.Equal(30, session.TrackingThresholdPercentage);
        Assert.Equal(60, session.TrackingThresholdSeconds);
    }

    [Theory]
    [InlineData(0.5, 2)]
    [InlineData(0.75, 3)]
    [InlineData(1.25, 5)]
    [InlineData(1.5, 6)]
    [InlineData(1.75, 7)]
    public async Task TrackWatchProgress_AccumulatesFractionalPlaybackRateWithoutPerChunkCeiling(
        double playbackRate,
        int expectedTrackedSeconds)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);

        for (var sequence = 1; sequence <= 4; sequence++)
        {
            var response = await handler.Handle(
                new TrackWatchProgressCommand(
                    fixture.Video.Id,
                    fixture.UserId,
                    fixture.SessionId,
                    ProgressSequence: sequence,
                    SecondsWatched: 1,
                    PlaybackRate: playbackRate,
                    TotalDurationSeconds: 100),
                CancellationToken.None);
            Assert.True(response.Success, response.Message);
        }

        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        var session = await db.VideoPlaybackSessions.SingleAsync();
        var rateKey = playbackRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var breakdown = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
            watchEvent.PlaybackRateBreakdownJson)!;

        Assert.Equal(expectedTrackedSeconds, watchEvent.TimeWatchedInSeconds);
        Assert.Equal(4m, watchEvent.ActualWatchedSeconds);
        Assert.Equal(4m, breakdown[rateKey]);
        Assert.Equal(0m, session.SpeedAdjustedSecondsRemainder);
    }

    [Theory]
    [InlineData(0.49, 1.5, 7, 0.35)]
    [InlineData(0.50, 0.5, 2, 0.50)]
    public async Task TrackWatchProgress_PreservesFractionalWallSecondsAcrossRequests(
        double secondsPerRequest,
        double playbackRate,
        int expectedTrackedSeconds,
        double expectedRemainder)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);

        for (var sequence = 1; sequence <= 10; sequence++)
        {
            var response = await handler.Handle(
                new TrackWatchProgressCommand(
                    fixture.Video.Id,
                    fixture.UserId,
                    fixture.SessionId,
                    ProgressSequence: sequence,
                    SecondsWatched: secondsPerRequest,
                    PlaybackRate: playbackRate,
                    TotalDurationSeconds: 1_000),
                CancellationToken.None);
            Assert.True(response.Success, response.Message);
        }

        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        var session = await db.VideoPlaybackSessions.SingleAsync();
        var rateKey = playbackRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var breakdown = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
            watchEvent.PlaybackRateBreakdownJson)!;

        Assert.Equal(expectedTrackedSeconds, watchEvent.TimeWatchedInSeconds);
        Assert.Equal((decimal)(secondsPerRequest * 10), watchEvent.ActualWatchedSeconds);
        Assert.Equal((decimal)(secondsPerRequest * 10), breakdown[rateKey]);
        Assert.Equal((decimal)expectedRemainder, session.SpeedAdjustedSecondsRemainder);
    }

    [Fact]
    public async Task TrackWatchProgress_BatchSkipsCommittedPrefixAndAppliesSuffix()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        session.CreatedAt = DateTime.UtcNow.AddSeconds(-30);
        await db.SaveChangesAsync();

        var first = await handler.Handle(Command(fixture, sequence: 1, seconds: 10), CancellationToken.None);
        var batch = await handler.Handle(BatchCommand(
            fixture,
            new WatchProgressSegment(1, 10, 1),
            new WatchProgressSegment(2, 10, 1)), CancellationToken.None);

        Assert.True(first.Success, first.Message);
        Assert.True(batch.Success, batch.Message);
        Assert.False(batch.Data!.Duplicate);
        Assert.Equal(20, batch.Data.TotalTrackedSeconds);
        Assert.Equal(2, session.LastProgressSequence);
        Assert.Equal(20m, session.AcceptedWallSeconds);
    }

    [Fact]
    public async Task TrackWatchProgress_BatchFirstMakesLateOriginalRequestIdempotent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        session.CreatedAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        var handler = CreateHandler(db);

        var batch = await handler.Handle(BatchCommand(
            fixture,
            new WatchProgressSegment(1, 15, 1),
            new WatchProgressSegment(2, 15, 1)), CancellationToken.None);
        var lateOriginal = await handler.Handle(
            Command(fixture, sequence: 1, seconds: 15),
            CancellationToken.None);

        Assert.True(batch.Success, batch.Message);
        Assert.True(batch.Data!.ViewRegistered);
        Assert.Equal(30, batch.Data.TotalTrackedSeconds);
        Assert.True(lateOriginal.Success, lateOriginal.Message);
        Assert.True(lateOriginal.Data!.Duplicate);
        Assert.False(lateOriginal.Data.ViewRegistered);
        Assert.True(lateOriginal.Data.SessionHasRegisteredView);
        Assert.Equal(30, lateOriginal.Data.TotalTrackedSeconds);
    }

    [Fact]
    public async Task TrackWatchProgress_BatchPreservesMixedRateSegmentsAndFractionalCarry()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        session.CreatedAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var response = await CreateHandler(db).Handle(BatchCommand(
            fixture,
            new WatchProgressSegment(1, 0.5, 0.5),
            new WatchProgressSegment(2, 0.5, 1.5)), CancellationToken.None);
        var watchEvent = await db.VideoWatchEvents.SingleAsync();
        var breakdown = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
            watchEvent.PlaybackRateBreakdownJson)!;

        Assert.True(response.Success, response.Message);
        Assert.Equal(1, response.Data!.TotalTrackedSeconds);
        Assert.Equal(1m, watchEvent.ActualWatchedSeconds);
        Assert.Equal(0.5m, breakdown["0.5"]);
        Assert.Equal(0.5m, breakdown["1.5"]);
        Assert.Equal(0m, session.SpeedAdjustedSecondsRemainder);
        Assert.Equal(1m, session.AcceptedWallSeconds);
    }

    [Fact]
    public async Task TrackWatchProgress_RejectsInvalidBatchWithoutMutation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);
        var invalidBatches = new (TrackWatchProgressCommand Command, string ErrorCode)[]
        {
            (BatchCommand(fixture), "PROGRESS_SEGMENTS_REQUIRED"),
            (BatchCommand(
                    fixture,
                    new WatchProgressSegment(1, 1, 1),
                    new WatchProgressSegment(3, 1, 1)),
                "PROGRESS_SEQUENCE_GAP"),
            (BatchCommand(
                    fixture,
                    new WatchProgressSegment(2, 1, 1),
                    new WatchProgressSegment(1, 1, 1)),
                "PROGRESS_SEQUENCE_GAP"),
            (BatchCommand(fixture, new WatchProgressSegment(1, 30.01, 1)), "PROGRESS_SECONDS_INVALID"),
            (BatchCommand(fixture, new WatchProgressSegment(1, 1, 1.1)), "PLAYBACK_RATE_INVALID"),
            (BatchCommand(fixture, new WatchProgressSegment(1, double.NaN, 1)), "PROGRESS_SECONDS_INVALID"),
            (BatchCommand(
                    fixture,
                    Enumerable.Range(1, 31)
                        .Select(sequence => new WatchProgressSegment(sequence, 1, 1))
                        .ToArray()),
                "PROGRESS_SEGMENTS_LIMIT_EXCEEDED")
        };

        foreach (var (command, errorCode) in invalidBatches)
        {
            var response = await handler.Handle(command, CancellationToken.None);
            Assert.False(response.Success);
            Assert.Contains(errorCode, response.Errors!);
        }

        Assert.Empty(db.VideoWatchEvents);
        Assert.Equal(0, (await db.VideoPlaybackSessions.SingleAsync()).LastProgressSequence);
    }

    [Fact]
    public async Task TrackWatchProgress_BatchCannotExceedElapsedSessionClock()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        session.CreatedAt = DateTime.UtcNow.AddSeconds(5);
        await db.SaveChangesAsync();

        var response = await CreateHandler(db).Handle(BatchCommand(
            fixture,
            new WatchProgressSegment(1, 30, 1),
            new WatchProgressSegment(2, 30, 1)), CancellationToken.None);
        var watchEvent = await db.VideoWatchEvents.SingleAsync();

        Assert.True(response.Success, response.Message);
        Assert.Equal(0, response.Data!.TotalTrackedSeconds);
        Assert.Equal(0m, watchEvent.ActualWatchedSeconds);
        Assert.Equal(0m, session.AcceptedWallSeconds);
        Assert.Equal(2, session.LastProgressSequence);
    }

    [Fact]
    public async Task TrackWatchProgress_NormalRequestCannotExceedSharedSessionClockAfterBatch()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var session = await db.VideoPlaybackSessions.SingleAsync();
        session.CreatedAt = DateTime.UtcNow.AddSeconds(5);
        await db.SaveChangesAsync();
        var handler = CreateHandler(db);

        var batch = await handler.Handle(BatchCommand(
            fixture,
            new WatchProgressSegment(1, 30, 1),
            new WatchProgressSegment(2, 30, 1)), CancellationToken.None);
        var normal = await handler.Handle(
            Command(fixture, sequence: 3, seconds: 30),
            CancellationToken.None);

        Assert.True(batch.Success, batch.Message);
        Assert.True(normal.Success, normal.Message);
        Assert.Equal(0, normal.Data!.TotalTrackedSeconds);
        Assert.Equal(0m, session.AcceptedWallSeconds);
        Assert.Equal(3, session.LastProgressSequence);
    }

    [Fact]
    public async Task TrackWatchProgress_BatchRejectsGapAfterCommittedSequence()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        var handler = CreateHandler(db);
        await handler.Handle(Command(fixture, sequence: 1, seconds: 1), CancellationToken.None);

        var response = await handler.Handle(BatchCommand(
            fixture,
            new WatchProgressSegment(3, 1, 1)), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("PROGRESS_SEQUENCE_GAP", response.Errors!);
        Assert.Equal(1, (await db.VideoPlaybackSessions.SingleAsync()).LastProgressSequence);
    }

    [Fact]
    public async Task CreateVideoSession_UsesGlobalThresholdWhenProviderSettingIsMissing()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 3);
        db.PlatformSettings.Add(new PlatformSetting
        {
            Key = PlatformSettingKeys.VideoWatchThresholdPercentage,
            Value = "47"
        });
        await db.SaveChangesAsync();

        var response = await new CreateVideoSessionCommandHandler(
                db,
                AllowAccess.Instance,
                FakeEncryption.Instance)
            .Handle(
                new CreateVideoSessionCommand(fixture.Video.Id, fixture.UserId),
                CancellationToken.None);
        var newestSession = await db.VideoPlaybackSessions
            .OrderByDescending(session => session.CreatedAt)
            .FirstAsync();

        Assert.True(response.Success, response.Message);
        Assert.Equal(47, response.Data!.ThresholdPercentage);
        Assert.Equal(47, newestSession.TrackingThresholdPercentage);
    }

    [Fact]
    public void VideoPlaybackSessionPolicy_BoundsUnknownShortAndExtremeDurations()
    {
        Assert.Equal(TimeSpan.FromHours(2), VideoPlaybackSessionPolicy.ResolveLifetime(null));
        Assert.Equal(TimeSpan.FromHours(2), VideoPlaybackSessionPolicy.ResolveLifetime(0));
        Assert.Equal(TimeSpan.FromMinutes(35), VideoPlaybackSessionPolicy.ResolveLifetime(5 * 60));
        Assert.Equal(TimeSpan.FromHours(4.5), VideoPlaybackSessionPolicy.ResolveLifetime(4 * 60 * 60));
        Assert.Equal(TimeSpan.FromHours(8), VideoPlaybackSessionPolicy.ResolveLifetime(int.MaxValue));
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

    [Fact]
    public async Task AdminPreview_BypassesMandatoryExamAndWatchLimit_WithoutExposingWatchState()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 1);
        db.Exams.Add(new Exam
        {
            Title = "Mandatory video exam",
            LessonVideoId = fixture.Video.Id,
            IsMandatory = true
        });
        db.VideoWatchEvents.Add(new VideoWatchEvent
        {
            UserId = fixture.UserId,
            LessonVideoId = fixture.Video.Id,
            WatchCount = 1,
            IsLocked = true
        });
        await db.SaveChangesAsync();

        var handler = new CreateVideoSessionCommandHandler(db, AllowAccess.Instance, FakeEncryption.Instance);
        var preview = await handler.Handle(
            new CreateVideoSessionCommand(fixture.Video.Id, fixture.UserId, Mode: VideoSessionMode.AdminPreview),
            CancellationToken.None);

        Assert.True(preview.Success, preview.Message);
        Assert.True(preview.Data!.IsPreview);
        Assert.Equal(0, preview.Data.WatchInfo.CurrentCount);
        Assert.Equal(0, preview.Data.WatchInfo.MaxCount);
        Assert.False(preview.Data.WatchInfo.IsLocked);
        Assert.True((await db.VideoWatchEvents.SingleAsync()).IsLocked);
    }

    [Fact]
    public async Task CreateVideoSession_RechecksWatchLimitAfterAcquiringPlaybackLock()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedFixtureAsync(db, maxWatchCount: 1);
        var sessionCountBeforeCreate = await db.VideoPlaybackSessions.CountAsync();
        var concurrency = new CallbackPlaybackConcurrency(async () =>
        {
            db.VideoWatchEvents.Add(new VideoWatchEvent
            {
                UserId = fixture.UserId,
                LessonVideoId = fixture.Video.Id,
                WatchCount = 1,
                TimeWatchedInSeconds = 30
            });
            await db.SaveChangesAsync();
        });
        var handler = new CreateVideoSessionCommandHandler(
            db,
            AllowAccess.Instance,
            FakeEncryption.Instance,
            playbackConcurrency: concurrency);

        var response = await handler.Handle(
            new CreateVideoSessionCommand(fixture.Video.Id, fixture.UserId),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("WATCH_LIMIT_REACHED", response.Errors!);
        Assert.True(response.Data!.WatchInfo.IsLocked);
        Assert.Equal(1, response.Data.WatchInfo.CurrentCount);
        Assert.Equal(sessionCountBeforeCreate, await db.VideoPlaybackSessions.CountAsync());
        Assert.True(concurrency.WasAcquired);
    }

    private static TrackWatchProgressCommandHandler CreateHandler(AppDbContext db) =>
        new(db, FixedSettingsReader.Default);

    private static TrackWatchProgressCommand Command(Fixture fixture, long sequence, double seconds) =>
        new(fixture.Video.Id, fixture.UserId, fixture.SessionId, sequence, seconds, 1, 100);

    private static TrackWatchProgressCommand BatchCommand(
        Fixture fixture,
        params WatchProgressSegment[] segments) =>
        new(
            fixture.Video.Id,
            fixture.UserId,
            fixture.SessionId,
            ProgressSequence: 0,
            SecondsWatched: 0,
            PlaybackRate: 1,
            TotalDurationSeconds: 100,
            ProgressSegments: segments);

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
        CreatedAt = DateTime.UtcNow.AddMinutes(-1),
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

    private sealed class CallbackPlaybackConcurrency(Func<Task> callback) : IVideoPlaybackConcurrency
    {
        public bool WasAcquired { get; private set; }

        public async Task AcquireAsync(
            Guid userId,
            Guid lessonVideoId,
            CancellationToken cancellationToken)
        {
            WasAcquired = true;
            await callback();
        }
    }

    private sealed class AllowAccess : IAccessCheckService
    {
        public static readonly AllowAccess Instance = new();
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default) => Task.FromResult(true);
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
