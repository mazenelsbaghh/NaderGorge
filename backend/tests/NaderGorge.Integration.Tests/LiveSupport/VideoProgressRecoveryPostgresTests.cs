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
