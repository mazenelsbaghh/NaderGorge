using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Domain.Entities;
using System.Security.Claims;

namespace NaderGorge.Application.Tests;

public sealed class VideoSessionControllerTests
{
    [Fact]
    public async Task GetEmbedMaterial_ConsumedButActiveSession_ReturnsMaterial()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        session.IsConsumed = true;
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new VideoSessionController(null!, db, NullLogger<VideoSessionController>.Instance);

        var response = await controller.GetEmbedMaterial(session.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var material = Assert.IsType<VideoEmbedMaterialResponse>(ok.Value);
        Assert.Equal(session.SessionToken, material.Token);
        Assert.Equal(session.EncryptionKey, material.Key);
    }

    [Fact]
    public async Task GetEmbedMaterial_SupersededSession_ReturnsNotFound()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        session.IsSuperseded = true;
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new VideoSessionController(null!, db, NullLogger<VideoSessionController>.Instance);

        var response = await controller.GetEmbedMaterial(session.Id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response);
    }

    [Fact]
    public async Task TrackProgress_AllowsUnknownDurationAndForwardsProgressSegments()
    {
        await using var db = TestAppDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var lessonVideoId = Guid.NewGuid();
        var mediator = new CapturingMediator();
        var controller = new VideoSessionController(mediator, db, NullLogger<VideoSessionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("id", userId.ToString()), new Claim(ClaimTypes.Role, "Student")],
                        "test"))
                }
            }
        };

        var response = await controller.TrackProgress(
            lessonVideoId,
            new TrackProgressRequest
            {
                SessionId = Guid.NewGuid(),
                TotalDurationSeconds = 0,
                ProgressSegments =
                [
                    new TrackProgressSegmentRequest
                    {
                        ProgressSequence = 1,
                        SecondsWatched = 0.5,
                        PlaybackRate = 1.5
                    }
                ]
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        Assert.NotNull(mediator.CapturedCommand);
        Assert.Equal(0, mediator.CapturedCommand.TotalDurationSeconds);
        var segment = Assert.Single(mediator.CapturedCommand.ProgressSegments!);
        Assert.Equal(1, segment.ProgressSequence);
        Assert.Equal(0.5, segment.SecondsWatched);
        Assert.Equal(1.5, segment.PlaybackRate);
    }

    [Fact]
    public async Task Incident20260903_OwnedHlsFailure_LogsBoundedDiagnostic()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();
        var logger = new CapturingLogger();
        var controller = StudentController(session.UserId, db, logger);

        var response = await controller.ReportClientEvent(
            session.Id,
            new VideoPlaybackClientEventRequest
            {
                Provider = "bunny-hls",
                Event = "playback-error",
                Phase = "manifestLoadError",
                StatusCode = 403
            },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(response);
        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("manifestLoadError", diagnostic, StringComparison.Ordinal);
        Assert.Contains("403", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("bcdn_token", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Incident20260903_DifferentStudentHlsSession_IsNotDisclosedOrLogged()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();
        var logger = new CapturingLogger();
        var controller = StudentController(Guid.NewGuid(), db, logger);

        var response = await controller.ReportClientEvent(
            session.Id,
            new VideoPlaybackClientEventRequest
            {
                Provider = "bunny-hls",
                Event = "playback-error",
                Phase = "fragLoadError",
                StatusCode = 0
            },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(response);
        Assert.Empty(logger.Messages);
    }

    private static VideoSessionController StudentController(
        Guid userId,
        NaderGorge.Infrastructure.Data.AppDbContext db,
        ILogger<VideoSessionController> logger) => new(null!, db, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("id", userId.ToString()), new Claim(ClaimTypes.Role, "Student")],
                        "test"))
                }
            }
        };

    private static VideoPlaybackSession ActiveSession() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        LessonVideoId = Guid.NewGuid(),
        SessionToken = "encrypted-token",
        EncryptionKey = "encryption-key",
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
    };

    private sealed class CapturingMediator : IMediator
    {
        public TrackWatchProgressCommand? CapturedCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            CapturedCommand = Assert.IsType<TrackWatchProgressCommand>(request);
            object response = ApiResponse<WatchProgressDto>.Ok(new WatchProgressDto(
                CurrentCount: 0,
                MaxCount: 5,
                IsLocked: false,
                ViewRegistered: false,
                SessionHasRegisteredView: false,
                TotalTrackedSeconds: 0,
                ThresholdSeconds: 30,
                SessionExpiresAt: DateTime.UtcNow.AddMinutes(5),
                Duplicate: false));
            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotImplementedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class CapturingLogger : ILogger<VideoSessionController>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
