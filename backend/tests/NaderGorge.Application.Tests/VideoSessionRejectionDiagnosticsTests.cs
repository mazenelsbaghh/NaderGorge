using System.Collections.Concurrent;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Authorization;
using NaderGorge.API.Controllers;
using NaderGorge.API.Observability;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Commands;
using Xunit.Abstractions;

namespace NaderGorge.Application.Tests;

public sealed class VideoSessionRejectionDiagnosticsTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(null)]
    [InlineData("student@example.invalid")]
    [InlineData("Authorization=Bearer synthetic-private-value")]
    [InlineData("https://example.invalid/video?token=synthetic-signature")]
    [InlineData("BUNNY_HLS_TIMEOUT\nstudent-private-value")]
    [InlineData("bunny_hls_timeout")]
    public void UntrustedReasons_EmitOnlyFixedFallbackAndNumbers(string? value)
    {
        var logger = new Recorder();
        new VideoSessionRejectionDiagnostics(new ManualClock()).Log(logger, value, 12.346);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(15001, entry.Event.Id);
        Assert.Null(entry.Exception);
        Assert.Equal("UNCLASSIFIED", entry.Fields["ReasonCode"]);
        Assert.Equal(12.35, entry.Fields["HandlerMs"]);
        Assert.Equal(0L, entry.Fields["Suppressed"]);
        Assert.Equal(["HandlerMs", "ReasonCode", "Suppressed", "{OriginalFormat}"], entry.Fields.Keys.Order(StringComparer.Ordinal).ToArray());
        if (value is not null) Assert.DoesNotContain(value, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConcurrentBurst_ProducesOneSampleAndReportsSuppressedCountAtNextMinute()
    {
        var clock = new ManualClock();
        var diagnostic = new VideoSessionRejectionDiagnostics(clock);
        var logger = new Recorder();
        Parallel.For(0, 10_000, _ => diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000));
        Assert.Single(logger.Entries);
        clock.Advance(TimeSpan.FromSeconds(59));
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Single(logger.Entries);
        clock.Advance(TimeSpan.FromSeconds(1));
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(10_000L, logger.Entries.Last().Fields["Suppressed"]);
    }

    [Fact]
    public void DistinctUnknownValues_ShareOneBucketWithoutRetainingValues()
    {
        var logger = new Recorder();
        var diagnostic = new VideoSessionRejectionDiagnostics(new ManualClock());
        for (var i = 0; i < 10_000; i++) diagnostic.Log(logger, $"synthetic-private-{i}", 1);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("UNCLASSIFIED", entry.Fields["ReasonCode"]);
        Assert.DoesNotContain("synthetic-private", entry.Message);
    }

    [Fact]
    public void DifferentKnownReasons_AreNotHiddenByAnotherReasonsBurst()
    {
        var diagnostic = new VideoSessionRejectionDiagnostics(new ManualClock());
        var logger = new Recorder();
        for (var i = 0; i < 100; i++) diagnostic.Log(logger, "ACCESS_DENIED", 10);
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal("BUNNY_HLS_TIMEOUT", logger.Entries.Last().Fields["ReasonCode"]);
    }

    [Fact]
    public async Task DelayedTimestampRead_CannotAdmitTwoSamplesAtTheSameCurrentTime()
    {
        using var clock = new PausingClock();
        var diagnostic = new VideoSessionRejectionDiagnostics(clock);
        var logger = new Recorder();
        var first = Task.Factory.StartNew(() => diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(clock.Captured.Wait(TimeSpan.FromSeconds(5)));
            clock.Advance(TimeSpan.FromSeconds(100));
        }
        finally { clock.Resume.Set(); }
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Single(logger.Entries);
        clock.Advance(TimeSpan.FromSeconds(59));
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Single(logger.Entries);
        clock.Advance(TimeSpan.FromSeconds(1));
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(2L, logger.Entries.Last().Fields["Suppressed"]);
    }

    [Fact]
    public async Task DelayedLogger_KeepsOneSampleInFlightAndStartsCooldownAfterCompletion()
    {
        var clock = new ManualClock();
        var diagnostic = new VideoSessionRejectionDiagnostics(clock);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var logger = new Recorder
        {
            BeforeLog = () =>
            {
                entered.Set();
                if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Synthetic logger timeout");
            }
        };
        var first = Task.Factory.StartNew(() => diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            clock.Advance(TimeSpan.FromSeconds(120));
            // A separate logger makes a duplicate admission observable without blocking the test.
            var duplicateLogger = new Recorder();
            Parallel.For(0, 10_000, _ => diagnostic.Log(duplicateLogger, "BUNNY_HLS_TIMEOUT", 8000));
            Assert.Empty(duplicateLogger.Entries);
            diagnostic.Log(duplicateLogger, "ACCESS_DENIED", 1);
            Assert.Equal("ACCESS_DENIED", Assert.Single(duplicateLogger.Entries).Fields["ReasonCode"]);
        }
        finally
        {
            resume.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Single(logger.Entries);
        clock.Advance(TimeSpan.FromSeconds(59));
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Single(logger.Entries);
        clock.Advance(TimeSpan.FromSeconds(1));
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(10_001L, logger.Entries.Last().Fields["Suppressed"]);
    }

    [Fact]
    public void AllFixedReasonBuckets_StayBoundedAcrossConcurrentMinuteBoundaries()
    {
        string[] reasons =
        [
            "UNCLASSIFIED", "VIDEO_NOT_FOUND", "ACCESS_DENIED", "WATCH_LIMIT_REACHED", "EXAM_LOCKED",
            "BUNNY_VIDEO_NOT_READY", "INVALID_PROVIDER", "YOUTUBE_HLS_SOURCE_INVALID", "BUNNY_LIBRARY_MISSING",
            "BUNNY_HLS_VALIDATION_UNAVAILABLE", "BUNNY_HLS_CONFIG_INCOMPLETE", "BUNNY_HLS_SIGNING_FAILED",
            "GIFT_LIMIT_REACHED", "BUNNY_HLS_VIDEO_INVALID", "BUNNY_HLS_TIMEOUT", "BUNNY_HLS_UNREACHABLE",
            "BUNNY_HLS_AUTH_REJECTED", "BUNNY_HLS_NATIVE_AUTH_REJECTED", "BUNNY_HLS_CORS_REJECTED",
            "BUNNY_HLS_MANIFEST_INVALID", "BUNNY_HLS_NOT_FOUND", "BUNNY_HLS_HTTP_ERROR"
        ];
        var clock = new ManualClock();
        var diagnostic = new VideoSessionRejectionDiagnostics(clock);
        var logger = new Recorder();
        Parallel.For(0, 220_000, i => diagnostic.Log(logger, reasons[i % reasons.Length], 1));
        Assert.Equal(22, logger.Entries.Count);
        Assert.Equal(reasons.Order(StringComparer.Ordinal),
            logger.Entries.Select(entry => (string)entry.Fields["ReasonCode"]!).Order(StringComparer.Ordinal));
        clock.Advance(TimeSpan.FromSeconds(59));
        Parallel.ForEach(reasons, code => diagnostic.Log(logger, code, 1));
        Assert.Equal(22, logger.Entries.Count);
        clock.Advance(TimeSpan.FromSeconds(1));
        Parallel.For(0, 220_000, i => diagnostic.Log(logger, reasons[i % reasons.Length], 1));
        Assert.Equal(44, logger.Entries.Count);
        Assert.All(logger.Entries.GroupBy(entry => entry.Fields["ReasonCode"]), group => Assert.Equal(2, group.Count()));
    }

    [Fact]
    public void FailedLogger_ReleasesInFlightReservationButPreservesCooldown()
    {
        var clock = new ManualClock();
        var diagnostic = new VideoSessionRejectionDiagnostics(clock);
        var logger = new Recorder { ThrowLog = true };
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 1);
        logger.ThrowLog = false;
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 1);
        Assert.Empty(logger.Entries);
        clock.Advance(TimeSpan.FromSeconds(60));
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 1);
        Assert.Equal(1L, Assert.Single(logger.Entries).Fields["Suppressed"]);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void InvalidDurations_AreFiniteZero(double value)
    {
        var logger = new Recorder();
        new VideoSessionRejectionDiagnostics(new ManualClock()).Log(logger, "BUNNY_HLS_TIMEOUT", value);
        Assert.Equal(0d, Assert.Single(logger.Entries).Fields["HandlerMs"]);
    }

    [Fact]
    public void DisabledLogger_DoesNotConsumeTheSampleOrReadClock()
    {
        var clock = new ManualClock();
        var diagnostic = new VideoSessionRejectionDiagnostics(clock);
        var logger = new Recorder { Enabled = false };
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 1);
        Assert.Equal(0, clock.Reads);
        logger.Enabled = true;
        diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 1);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task Controller_DoesNotLogResponseBodySecondaryErrorsOrIdentity()
    {
        await using var db = TestAppDbContextFactory.Create();
        var response = ApiResponse<VideoSessionDto>.Fail("synthetic-private-body", ["synthetic-private-first", "synthetic-private-second"]);
        var mediator = new ResponseMediator(response);
        var logger = new Recorder();
        var controller = Controller(db, mediator, logger);
        var result = Assert.IsType<BadRequestObjectResult>(await controller.CreateSession(new() { LessonVideoId = Guid.NewGuid() }, default));
        Assert.Same(response, result.Value);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("UNCLASSIFIED", entry.Fields["ReasonCode"]);
        Assert.DoesNotContain("synthetic-private", entry.Message);
        Assert.DoesNotContain(controller.User.FindFirst("id")!.Value, entry.Message);
        Assert.Null(entry.Exception);
        Assert.Empty(db.VideoPlaybackSessions);
    }

    [Theory]
    [InlineData("VIDEO_NOT_FOUND", 404)]
    [InlineData("ACCESS_DENIED", 403)]
    [InlineData("WATCH_LIMIT_REACHED", 400)]
    [InlineData("EXAM_LOCKED", 409)]
    [InlineData("BUNNY_VIDEO_NOT_READY", 409)]
    [InlineData("BUNNY_HLS_TIMEOUT", 400)]
    [InlineData("BUNNY_HLS_AUTH_REJECTED", 400)]
    [InlineData("GIFT_LIMIT_REACHED", 400)]
    [InlineData("unexpected", 400)]
    public async Task Controller_RejectionsPreserveExactResponseAndStatus(string code, int status)
    {
        await using var db = TestAppDbContextFactory.Create();
        var response = ApiResponse<VideoSessionDto>.Fail("unchanged response", [code]);
        var mediator = new ResponseMediator(response);
        var controller = Controller(db, mediator, NullLogger<VideoSessionController>.Instance);
        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.CreateSession(new() { LessonVideoId = Guid.NewGuid() }, default));
        Assert.Equal(status, result.StatusCode);
        Assert.Same(response, result.Value);
        Assert.Equal(1, mediator.Calls);
    }

    [Fact]
    public async Task Controller_SuccessIsUnchangedAndDoesNotTouchLogger()
    {
        await using var db = TestAppDbContextFactory.Create();
        var response = ApiResponse<VideoSessionDto>.Ok(null!);
        var mediator = new ResponseMediator(response);
        var logger = new Recorder();
        var controller = Controller(db, mediator, logger);
        var result = Assert.IsType<OkObjectResult>(await controller.CreateSession(new() { LessonVideoId = Guid.NewGuid() }, default));
        Assert.Same(response, result.Value);
        Assert.Equal(0, logger.EnabledChecks);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailingLogger_CannotChangeRejectionResponse(bool failDuringEnabledCheck)
    {
        await using var db = TestAppDbContextFactory.Create();
        var response = ApiResponse<VideoSessionDto>.Fail("unchanged", ["BUNNY_HLS_SIGNING_FAILED"]);
        var logger = new Recorder { ThrowEnabled = failDuringEnabledCheck, ThrowLog = !failDuringEnabledCheck };
        var result = Assert.IsType<BadRequestObjectResult>(await Controller(db, new(response), logger).CreateSession(new() { LessonVideoId = Guid.NewGuid() }, default));
        Assert.Same(response, result.Value);
    }

    [Fact]
    public async Task MissingIdentity_RejectsBeforeMediatorAndDiagnostics()
    {
        await using var db = TestAppDbContextFactory.Create();
        var mediator = new ResponseMediator(ApiResponse<VideoSessionDto>.Ok(null!));
        var logger = new Recorder();
        var controller = Controller(db, mediator, logger);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsType<UnauthorizedResult>(await controller.CreateSession(new() { LessonVideoId = Guid.NewGuid() }, default));
        Assert.Equal(0, mediator.Calls);
        Assert.Equal(0, logger.EnabledChecks);
    }

    [Fact]
    public async Task TeacherWithoutWorkspace_StillForbiddenBeforeMediatorAndDiagnostics()
    {
        await using var db = TestAppDbContextFactory.Create();
        var mediator = new ResponseMediator(ApiResponse<VideoSessionDto>.Ok(null!));
        var logger = new Recorder();
        var controller = Controller(db, mediator, logger);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("id", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Teacher")], "test"));
        Assert.IsType<ForbidResult>(await controller.CreateSession(new() { LessonVideoId = Guid.NewGuid() }, default));
        Assert.Equal(0, mediator.Calls);
        Assert.Equal(0, logger.EnabledChecks);
    }

    [Fact]
    public void Endpoint_StillRequiresPlaybackAuthorizationAndOriginalRateLimit()
    {
        var attributes = typeof(VideoSessionController).GetCustomAttributes(false);
        Assert.Equal(VideoPlaybackAuthorization.Policy, Assert.Single(attributes.OfType<AuthorizeAttribute>()).Policy);
        Assert.Equal("video-session", Assert.Single(attributes.OfType<EnableRateLimitingAttribute>()).PolicyName);
    }

    [Fact]
    public void SuppressedPath_HasNoPerRejectionAllocationAndBoundedOutput()
    {
        const int count = 200_000;
        var diagnostic = new VideoSessionRejectionDiagnostics(new ManualClock());
        var logger = new Recorder();
        for (var i = 0; i < 10_000; i++) diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        var watch = new System.Diagnostics.Stopwatch();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (var i = 0; i < count; i++) diagnostic.Log(logger, "BUNNY_HLS_TIMEOUT", 8000);
        watch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Single(logger.Entries);
        Assert.Equal(0L, allocated);
        output.WriteLine($"SuppressedCalls={count} ElapsedMs={watch.Elapsed.TotalMilliseconds:F3} NanosecondsPerCall={watch.Elapsed.TotalNanoseconds / count:F2} AllocatedBytes={allocated} Samples={logger.Entries.Count}");
    }

    [Fact]
    public async Task MediatorCancellation_PropagatesWithoutDiagnosticLogging()
    {
        await using var db = TestAppDbContextFactory.Create();
        var mediator = new ResponseMediator(ApiResponse<VideoSessionDto>.Ok(null!)) { ThrowCancellation = true };
        var logger = new Recorder();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Controller(db, mediator, logger)
            .CreateSession(new() { LessonVideoId = Guid.NewGuid() }, cancellation.Token));
        Assert.Equal(0, logger.EnabledChecks);
        Assert.Empty(logger.Entries);
    }

    private static VideoSessionController Controller(NaderGorge.Infrastructure.Data.AppDbContext db, ResponseMediator mediator, ILogger<VideoSessionController> logger) => new(mediator, db, logger)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("id", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Student")], "test"))
            }
        }
    };

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        public int Reads { get; private set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() { Reads++; return Interlocked.Read(ref _timestamp); }
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _timestamp, duration.Ticks);
    }

    private sealed class PausingClock : TimeProvider, IDisposable
    {
        private long _timestamp;
        private int _reads;
        public ManualResetEventSlim Captured { get; } = new();
        public ManualResetEventSlim Resume { get; } = new();
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp()
        {
            var value = Interlocked.Read(ref _timestamp);
            if (Interlocked.Increment(ref _reads) == 1)
            {
                Captured.Set();
                if (!Resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Synthetic clock timeout");
            }
            return value;
        }
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _timestamp, duration.Ticks);
        public void Dispose() { Captured.Dispose(); Resume.Dispose(); }
    }

    private sealed record Entry(LogLevel Level, EventId Event, Dictionary<string, object?> Fields, string Message, Exception? Exception);
    private sealed class Recorder : ILogger<VideoSessionController>
    {
        public ConcurrentQueue<Entry> Entries { get; } = new();
        public bool Enabled { get; set; } = true;
        public bool ThrowEnabled { get; set; }
        public bool ThrowLog { get; set; }
        public Action? BeforeLog { get; init; }
        public int EnabledChecks;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level)
        {
            Interlocked.Increment(ref EnabledChecks);
            if (ThrowEnabled) throw new InvalidOperationException("synthetic-private-logger-exception");
            return Enabled;
        }
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (ThrowLog) throw new InvalidOperationException("synthetic-private-logger-exception");
            BeforeLog?.Invoke();
            var fields = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(x => x.Key, x => x.Value);
            Entries.Enqueue(new(level, eventId, fields, formatter(state, exception), exception));
        }
    }

    private sealed class ResponseMediator(ApiResponse<VideoSessionDto> response) : IMediator
    {
        public int Calls { get; private set; }
        public bool ThrowCancellation { get; init; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Assert.IsType<CreateVideoSessionCommand>(request);
            Calls++;
            if (ThrowCancellation) throw new OperationCanceledException(cancellationToken);
            return Task.FromResult((TResponse)(object)response);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
