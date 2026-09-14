using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using Npgsql;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class VideoProgressRecoveryPostgresTests
{
    [Fact]
    public async Task FullDurationProgress_PersistsBeyondQuota_AndFeedsStudentPercentages()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var session = await SeedSessionAsync(fixture.Db);
        session.CreatedAt = DateTime.UtcNow.AddMinutes(-5);
        await fixture.Db.SaveChangesAsync();
        var handler = new TrackWatchProgressCommandHandler(fixture.Db, new Settings(), new PostgresVideoPlaybackConcurrency(fixture.Db));
        var scope = new StudentLessonCompletionContext(fixture.Db, session.UserId, [session.LessonVideo.LessonId]);
        var first = await handler.Handle(new TrackWatchProgressCommand(session.LessonVideoId, session.UserId, session.Id, 1, 30, 2, 100), CancellationToken.None);
        Assert.True(first.Success, first.Message);
        Assert.Equal(60m, first.Data!.LearningWatchedSeconds);
        Assert.True(first.Data.SessionHasRegisteredView);
        var partial = Assert.Single(await StudentWatchProgressReader.ReadAsync(scope, [session.LessonVideoId], CancellationToken.None));
        Assert.False(partial.IsCompleted);
        Assert.Equal(60, StudentWatchProgressReader.CalculatePercent([partial]));
        // Incident 2026-09-12: queued progress arrives immediately after the
        // preceding acknowledgement; do not artificially age UpdatedAt.
        fixture.Db.ChangeTracker.Clear();
        var complete = await handler.Handle(new TrackWatchProgressCommand(session.LessonVideoId, session.UserId, session.Id, 2, 20, 2, 100), CancellationToken.None);
        Assert.True(complete.Success, complete.Message);
        Assert.Equal(100m, complete.Data!.LearningWatchedSeconds);
        Assert.Equal(1, complete.Data.CurrentCount);
        fixture.Db.ChangeTracker.Clear();
        var saved = Assert.Single(await StudentWatchProgressReader.ReadAsync(scope, [session.LessonVideoId], CancellationToken.None));
        Assert.True(saved.IsCompleted);
        Assert.Equal(100, StudentWatchProgressReader.CalculatePercent([saved]));
        Assert.NotNull(saved.LastWatchedAt);
        var unrelated = scope with { UserId = Guid.NewGuid() };
        var other = Assert.Single(await StudentWatchProgressReader.ReadAsync(unrelated, [session.LessonVideoId], CancellationToken.None));
        Assert.Equal(0m, other.WatchedSeconds);
        Assert.False(other.IsCompleted);
        Assert.Null(StudentWatchProgressReader.CalculatePercent([other]));
    }

    [Fact]
    public async Task Incident20260912_DelayedSinglesPreserveFullLearningTimeAndRemainIdempotent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var session = await SeedSessionAsync(fixture.Db);
        session.CreatedAt = DateTime.UtcNow.AddMinutes(-5);
        await fixture.Db.SaveChangesAsync();
        var handler = new TrackWatchProgressCommandHandler(fixture.Db, new Settings(), new PostgresVideoPlaybackConcurrency(fixture.Db));

        for (var sequence = 1; sequence <= 4; sequence++)
        {
            var command = new TrackWatchProgressCommand(session.LessonVideoId, session.UserId, session.Id,
                sequence, sequence == 4 ? 10 : 30, 1, 100);
            var accepted = await handler.Handle(command, CancellationToken.None);
            var replay = await handler.Handle(command, CancellationToken.None);
            Assert.True(accepted.Success, accepted.Message);
            Assert.True(replay.Data!.Duplicate);
            Assert.Equal(Math.Min(sequence * 30, 100), replay.Data.LearningWatchedSeconds);
        }

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.VideoWatchEvents.SingleAsync(watch => watch.UserId == session.UserId);
        Assert.Equal(100m, saved.LearningWatchedSeconds);
        Assert.Equal(1, saved.WatchCount);
        Assert.Equal(100m, (await fixture.Db.VideoPlaybackSessions.SingleAsync(s => s.Id == session.Id)).AcceptedWallSeconds);
    }

    [Fact]
    public async Task HistoricalCorrectionSuppliesMissingDurationWithoutChangingOtherStudentsOrQuota()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var session = await SeedSessionAsync(fixture.Db);
        session.TrackingDurationSeconds = null;
        fixture.Db.VideoWatchEvents.Add(new VideoWatchEvent
        {
            UserId = session.UserId, LessonVideoId = session.LessonVideoId,
            LearningWatchedSeconds = 95, LearningDurationSeconds = 100,
            TimeWatchedInSeconds = 12, ActualWatchedSeconds = 6, WatchCount = 1
        });
        await fixture.Db.SaveChangesAsync();
        var scope = new StudentLessonCompletionContext(fixture.Db, session.UserId, [session.LessonVideo.LessonId]);
        var corrected = await StudentWatchProgressReader.ReadAsync(scope, [session.LessonVideoId], default);
        Assert.Equal(95, StudentWatchProgressReader.CalculatePercent(corrected));
        var unrelated = await StudentWatchProgressReader.ReadAsync(scope with { UserId = Guid.NewGuid() }, [session.LessonVideoId], default);
        Assert.Null(StudentWatchProgressReader.CalculatePercent(unrelated));
        session.TrackingDurationSeconds = 200;
        await fixture.Db.SaveChangesAsync();
        var actual = await StudentWatchProgressReader.ReadAsync(scope, [session.LessonVideoId], default);
        Assert.Equal(47, StudentWatchProgressReader.CalculatePercent(actual));
        var watch = await fixture.Db.VideoWatchEvents.SingleAsync(watch => watch.UserId == session.UserId);
        Assert.Equal(1, watch.WatchCount);
        Assert.Equal(12, watch.TimeWatchedInSeconds);
        Assert.Equal(6, watch.ActualWatchedSeconds);
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("commit-ack")]
    [InlineData("persistent-lock")]
    public async Task Incident20260906_ConnectionFailure_RetriesWithoutDoubleCountingProgress(string failurePoint)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var session = await SeedSessionAsync(fixture.Db);
        IInterceptor failure = failurePoint == "commit-ack"
            ? new LostCommitAcknowledgement()
            : new InterruptedLock(failurePoint == "persistent-lock" ? int.MaxValue : 1);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString).AddInterceptors(failure).Options);
        var handler = new TrackWatchProgressCommandHandler(db, new Settings(), new PostgresVideoPlaybackConcurrency(db));
        var command = new TrackWatchProgressCommand(session.LessonVideoId, session.UserId, session.Id, 1, 30, 1, 100);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        if (failurePoint == "persistent-lock")
        {
            await Assert.ThrowsAsync<NpgsqlException>(() => handler.Handle(command, deadline.Token));
            Assert.False(await fixture.Db.VideoWatchEvents.AnyAsync(item => item.UserId == session.UserId));
            return;
        }

        var response = await handler.Handle(command, deadline.Token);
        var repeated = await handler.Handle(command, deadline.Token);

        Assert.True(response.Success);
        Assert.True(repeated.Success);
        Assert.True(repeated.Data!.Duplicate);
        Assert.Equal(1, repeated.Data.CurrentCount);
        Assert.Equal(30, repeated.Data.TotalTrackedSeconds);
        fixture.Db.ChangeTracker.Clear();
        var watch = await fixture.Db.VideoWatchEvents.SingleAsync(item => item.UserId == session.UserId);
        Assert.Equal(1, watch.WatchCount);
        Assert.Equal(30, watch.TimeWatchedInSeconds);
        var persistedSession = await fixture.Db.VideoPlaybackSessions.SingleAsync(item => item.Id == session.Id);
        Assert.Equal(1, persistedSession.LastProgressSequence);
        Assert.True(persistedSession.HasRegisteredView);
    }

    private static async Task<VideoPlaybackSession> SeedSessionAsync(AppDbContext db)
    {
        var subject = new Subject { Name = "Recovery", NormalizedName = Guid.NewGuid().ToString("N") };
        var package = new Package
        {
            Name = "Recovery", Subject = subject,
            Teacher = new TeacherProfile { User = User("Teacher") }
        };
        var section = new ContentSection { Title = "Recovery", Term = new Term { Title = "Recovery", Package = package } };
        var video = new LessonVideo
        {
            Title = "Recovery", Lesson = new Lesson { Title = "Recovery", ContentSection = section },
            Provider = "youtube", ProviderVideoId = "recovery", MaxWatchCount = 3,
            VideoTypeId = await db.VideoTypes.Select(type => type.Id).FirstAsync()
        };
        var session = new VideoPlaybackSession
        {
            User = User("Student"), LessonVideo = video, SessionToken = Guid.NewGuid().ToString("N"), EncryptionKey = "test",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1), ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            TrackingDurationSeconds = 100, TrackingThresholdPercentage = 30, TrackingThresholdSeconds = 30
        };
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private static User User(string name) => new()
    {
        FullName = name, PasswordHash = "not-used",
        PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}"
    };

    private sealed class Settings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken ct) => Task.FromResult(CachedPlatformSettings.Default);
        public void Invalidate() { }
    }

    private sealed class InterruptedLock(int remainingFailures) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) && remainingFailures-- > 0)
                throw new NpgsqlException("Exception while reading from stream", new IOException("connection interrupted"));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        private bool _failed;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new NpgsqlException("Exception while reading commit acknowledgement", new IOException("connection interrupted"));
            }
            return Task.CompletedTask;
        }
    }
}
