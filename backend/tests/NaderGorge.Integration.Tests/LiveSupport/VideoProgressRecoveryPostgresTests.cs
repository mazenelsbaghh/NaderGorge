using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Features.Parent.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using Npgsql;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class VideoProgressRecoveryPostgresTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public async Task HalfWatchedVideo_PersistsAcrossSessionsAndResumesCorrectlyAtEveryPlaybackRate(double rate)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var session = await SeedSessionAsync(fixture.Db);
        session.CreatedAt = DateTime.UtcNow.AddMinutes(-20);
        session.TrackingDurationSeconds = 600;
        session.TrackingThresholdSeconds = 180;
        await fixture.Db.SaveChangesAsync();
        var handler = new TrackWatchProgressCommandHandler(fixture.Db, new Settings(), new PostgresVideoPlaybackConcurrency(fixture.Db));
        await TrackHalfVideoAsync(handler, session, rate);
        await using (var reopened = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options))
        {
            var half = Assert.Single(await StudentWatchProgressReader.ReadAsync(
                new(reopened, session.UserId, [session.LessonVideo.LessonId]), [session.LessonVideoId], default));
            Assert.Equal(300m, half.WatchedSeconds);
            Assert.Equal(50, StudentWatchProgressReader.CalculatePercent([half]));
            Assert.False(half.IsCompleted);
        }
        session.IsSuperseded = true;
        var resumed = new VideoPlaybackSession
        {
            UserId = session.UserId, LessonVideoId = session.LessonVideoId,
            SessionToken = Guid.NewGuid().ToString("N"), EncryptionKey = "test",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10), ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            TrackingDurationSeconds = 600, TrackingThresholdPercentage = 30, TrackingThresholdSeconds = 180
        };
        fixture.Db.VideoPlaybackSessions.Add(resumed);
        await fixture.Db.SaveChangesAsync();
        await TrackHalfVideoAsync(handler, resumed, rate);
        fixture.Db.ChangeTracker.Clear();
        var complete = Assert.Single(await StudentWatchProgressReader.ReadAsync(
            new(fixture.Db, session.UserId, [session.LessonVideo.LessonId]), [session.LessonVideoId], default));
        Assert.Equal(600m, complete.WatchedSeconds);
        Assert.Equal(100, StudentWatchProgressReader.CalculatePercent([complete]));
        Assert.True(complete.IsCompleted);
    }

    [Fact]
    public async Task FourParts_WeightActualDurationAndAgreeAcrossStudentAndParentReports()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var firstSession = await SeedSessionAsync(fixture.Db);
        var lesson = firstSession.LessonVideo.Lesson;
        var package = lesson.ContentSection.Term.Package;
        package.Teacher.IsContentVisibleToStudents = true;
        var profile = new StudentProfile
        {
            User = firstSession.User, DateOfBirth = new DateTime(2008, 1, 1),
            Governorate = "Cairo", Address = "Test", EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        };
        fixture.Db.AddRange(profile,
            new StudentAccessGrant { UserId = firstSession.UserId, PackageId = package.Id, GrantType = CodeType.Package, IsActive = true },
            new StudentFacingAcademicScope { OwnerId = package.Id, OwnerType = StudentFacingScopeOwnerType.Package, ScopeLevel = AcademicScopeLevel.PlatformWide });
        var sessions = new List<VideoPlaybackSession> { firstSession };
        foreach (var duration in new[] { 130, 370, 400 })
        {
            var video = new LessonVideo { Lesson = lesson, Title = $"Part {duration}", Provider = "youtube",
                ProviderVideoId = Guid.NewGuid().ToString("N"), VideoTypeId = firstSession.LessonVideo.VideoTypeId };
            var session = new VideoPlaybackSession { UserId = firstSession.UserId, LessonVideo = video,
                TrackingDurationSeconds = duration, TrackingThresholdPercentage = 30,
                TrackingThresholdSeconds = VideoWatchProgressCalculator.ResolveThresholdSeconds(duration, 30),
                SessionToken = Guid.NewGuid().ToString("N"), EncryptionKey = "test",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10), ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
            fixture.Db.VideoPlaybackSessions.Add(session);
            sessions.Add(session);
        }
        firstSession.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
        await fixture.Db.SaveChangesAsync();
        var handler = new TrackWatchProgressCommandHandler(fixture.Db, new Settings(), new PostgresVideoPlaybackConcurrency(fixture.Db));
        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            var remaining = session.TrackingDurationSeconds!.Value;
            long sequence = 1;
            while (remaining > 0)
            {
                var seconds = Math.Min(remaining, 30);
                var command = new TrackWatchProgressCommand(session.LessonVideoId, session.UserId, session.Id, sequence++, seconds, 1, session.TrackingDurationSeconds.Value);
                var accepted = await handler.Handle(command, default);
                var repeated = await handler.Handle(command, default);
                Assert.True(accepted.Success, accepted.Message);
                Assert.True(repeated.Data!.Duplicate);
                Assert.Equal(accepted.Data!.LearningWatchedSeconds, repeated.Data.LearningWatchedSeconds);
                remaining -= seconds;
            }
            if (index is not (1 or 3)) continue;
            fixture.Db.ChangeTracker.Clear();
            var student = await new GetMyLessonsQueryHandler(fixture.Db, new AcademicScopeService(fixture.Db))
                .Handle(new GetMyLessonsQuery(firstSession.UserId), default);
            var parent = await new GetStudentAcademicDetailsQueryHandler(fixture.Db, new AcademicScopeService(fixture.Db), new ContentArchiveAccessService(fixture.Db))
                .Handle(new GetStudentAcademicDetailsQuery(profile.Id), default);
            Assert.True(student.Success, student.Message);
            Assert.True(parent.Success, parent.Message);
            var studentLesson = Assert.Single(student.Data!);
            var parentLesson = Assert.Single(parent.Data!.WatchLessons);
            Assert.Equal(4, studentLesson.VideoCount);
            Assert.Equal(index == 1 ? 2 : 4, studentLesson.WatchedVideoCount);
            Assert.Equal(index == 1 ? 23 : 100, studentLesson.WatchProgressPercent);
            Assert.Equal(index == 1 ? 230 : 1000, studentLesson.RecordedWatchSeconds);
            Assert.Equal(1000, studentLesson.TotalVideoSeconds);
            Assert.Equal(index == 3, studentLesson.IsCompleted);
            Assert.Equal(studentLesson.WatchedVideoCount, parentLesson.WatchedVideos);
            Assert.Equal(studentLesson.RecordedWatchSeconds, parentLesson.WatchedSeconds);
            Assert.Equal(studentLesson.IsCompleted, parentLesson.IsCompleted);
            Assert.Equal(studentLesson.WatchProgressPercent, parent.Data.Attendance.WatchProgressPercentage);
        }
    }

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
        // Duration must survive deletion of transient playback sessions.
        await fixture.Db.VideoPlaybackSessions.Where(playback => playback.Id == session.Id).ExecuteDeleteAsync();
        var retained = Assert.Single(await StudentWatchProgressReader.ReadAsync(scope, [session.LessonVideoId], CancellationToken.None));
        Assert.Equal(100, retained.DurationSeconds);
        Assert.True(retained.IsCompleted);
        Assert.Equal(100, StudentWatchProgressReader.CalculatePercent([retained]));
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

    private static async Task TrackHalfVideoAsync(TrackWatchProgressCommandHandler handler, VideoPlaybackSession session, double rate)
    {
        var remainingWallSeconds = session.TrackingDurationSeconds!.Value / 2m / (decimal)rate;
        long sequence = 1;
        while (remainingWallSeconds > 0)
        {
            var seconds = Math.Min(30m, remainingWallSeconds);
            var response = await handler.Handle(new TrackWatchProgressCommand(
                session.LessonVideoId, session.UserId, session.Id, sequence++, (double)seconds, rate,
                session.TrackingDurationSeconds.Value), default);
            Assert.True(response.Success, response.Message);
            remainingWallSeconds -= seconds;
        }
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
