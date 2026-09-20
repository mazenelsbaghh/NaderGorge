using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Domain.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using NaderGorge.Application.Services;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

public sealed class VideoSessionControllerTests
{
    [Fact]
    public async Task GetEmbedMaterial_ConsumedButActiveSession_ReturnsMaterial()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        await SeedPlaybackAccessAsync(db, session);
        session.IsConsumed = true;
        var encryption = new VideoEncryptionService();
        session.EncryptionKey = encryption.GenerateSessionKey();
        session.SessionToken = encryption.EncryptVideoInfo("youtube", "example", session.EncryptionKey);
        db.VideoPlaybackSessions.Add(session);
        db.PlatformSettings.Add(new PlatformSetting { Key = PlatformSettingKeys.WatermarkShowName, Value = "false" });
        await db.SaveChangesAsync();

        var controller = StudentController(session.UserId, db, NullLogger<VideoSessionController>.Instance);

        var service = new VideoSessionMaterialService(db, encryption, new BunnyHlsUrlSigner(),
            new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider()));
        var response = await controller.GetEmbedMaterial(session.Id, service, new AccessCheckService(db), true, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var material = Assert.IsType<VideoEmbedMaterialResponse>(ok.Value);
        Assert.Equal(session.SessionToken, material.Token);
        Assert.Equal(session.EncryptionKey, material.Key);
        Assert.Equal("false", material.WatermarkSettings?[PlatformSettingKeys.WatermarkShowName]);
        Assert.Equal(session.UserId.ToString(), material.StudentId);
        Assert.Equal(session.ExpiresAt, material.ExpiresAt);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetEmbedMaterial_SupersededSession_ReturnsConflictWithoutIssuingMaterial()
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        session.IsSuperseded = true;
        db.VideoPlaybackSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = StudentController(session.UserId, db, NullLogger<VideoSessionController>.Instance);

        var response = await controller.GetEmbedMaterial(session.Id, null!, new AccessCheckService(db), false, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response);
    }

    [Theory]
    [InlineData("different-user", 404)]
    [InlineData("anonymous", 401)]
    [InlineData("revoked-grant", 403)]
    [InlineData("inactive-account", 403)]
    [InlineData("expired", 410)]
    public async Task GetEmbedMaterial_DeniesUnauthorizedSessionBeforeIssuingMaterial(string denial, int expectedStatus)
    {
        await using var db = TestAppDbContextFactory.Create();
        var session = ActiveSession();
        var (user, grant) = await SeedPlaybackAccessAsync(db, session);
        db.VideoPlaybackSessions.Add(session);
        if (denial == "revoked-grant") grant.IsActive = false;
        if (denial == "inactive-account") user.IsActive = false;
        if (denial == "expired") session.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        var controller = StudentController(denial == "different-user" ? Guid.NewGuid() : session.UserId,
            db, NullLogger<VideoSessionController>.Instance);
        if (denial == "anonymous") controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.GetEmbedMaterial(session.Id, null!, new AccessCheckService(db), true, CancellationToken.None);

        switch (expectedStatus)
        {
            case 401: Assert.IsType<UnauthorizedResult>(result); break;
            case 403: Assert.IsType<ForbidResult>(result); break;
            case 404: Assert.IsType<NotFoundObjectResult>(result); break;
            case 410: Assert.Equal(410, Assert.IsType<ObjectResult>(result).StatusCode); break;
        }
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    private static async Task<(User User, StudentAccessGrant Grant)> SeedPlaybackAccessAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db, VideoPlaybackSession session)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Playback student", Guid.NewGuid().ToString());
        session.UserId = user.Id;
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Playback package");
        var term = new Term { Title = "Term", PackageId = packageId };
        var section = new ContentSection { Title = "Section", Term = term };
        var lesson = new Lesson { Title = "Lesson", Summary = "Lesson", ContentSection = section };
        var video = new LessonVideo { Id = session.LessonVideoId, Title = "Video", Provider = "youtube", ProviderVideoId = "example", Lesson = lesson, IsActive = true };
        var grant = new StudentAccessGrant { UserId = user.Id, GrantType = Domain.Enums.CodeType.Video, LessonVideoId = video.Id, IsActive = true };
        db.AddRange(term, section, lesson, video, grant);
        await db.SaveChangesAsync();
        return (user, grant);
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
    public async Task Incident20260904_OwnedBunnyBridgeTimeout_LogsBoundedDiagnostic()
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
                Provider = "bunny",
                Event = "bridge-timeout",
                Phase = "readiness_deadline",
                StatusCode = 0
            },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(response);
        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("bridge-timeout", diagnostic, StringComparison.Ordinal);
        Assert.Contains("readiness_deadline", diagnostic, StringComparison.Ordinal);
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
