using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Configuration;
using NaderGorge.API.Authorization;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.API.Filters;

using Microsoft.AspNetCore.RateLimiting;
using System.Text.RegularExpressions;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/student/video-session")]
[Authorize(Policy = VideoPlaybackAuthorization.Policy)]
[EnableRateLimiting("video-session")]
public class VideoSessionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly ILogger<VideoSessionController> _logger;

    public VideoSessionController(
        IMediator mediator,
        IAppDbContext db,
        ILogger<VideoSessionController> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateVideoSessionRequest request, CancellationToken ct)
    {
        // Get user ID from claims (custom extension logic or fallback to generic)
        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        if (User.IsInRole("Teacher") && !User.IsInRole("Admin"))
        {
            var lessonId = await _db.LessonVideos
                .Where(video => video.Id == request.LessonVideoId)
                .Select(video => (Guid?)video.LessonId)
                .SingleOrDefaultAsync(ct);
            var teacherAuthorization = new NaderGorge.Application.Services.TeacherAuthorizationService(_db);
            var workspace = await teacherAuthorization.GetWorkspaceAccessAsync(userId, ct);
            if (workspace is null || lessonId is null
                || !await teacherAuthorization.CanAccessLessonAsync(userId, lessonId.Value, ct))
                return Forbid();
        }

        var command = new CreateVideoSessionCommand(
            request.LessonVideoId,
            userId,
            GetIpAddress(),
            VideoPlaybackAuthorization.CanPreview(User)
                ? VideoSessionMode.AdminPreview : VideoSessionMode.Standard
        );

        var result = await _mediator.Send(command, ct);

        if (result.Success)
            return Ok(result);

        // Map common errors
        if (result.Errors != null && result.Errors.Contains("VIDEO_NOT_FOUND")) return NotFound(result);
        if (result.Errors != null && result.Errors.Contains("ACCESS_DENIED"))
            return StatusCode(StatusCodes.Status403Forbidden, result);
        if (result.Errors != null && result.Errors.Contains("WATCH_LIMIT_REACHED")) return BadRequest(result);
        if (result.Errors != null && (result.Errors.Contains("EXAM_LOCKED") || result.Errors.Contains("BUNNY_VIDEO_NOT_READY")))
            return Conflict(result);

        return BadRequest(result);
    }

    [HttpPost("{sessionId}/consume")]
    public async Task<IActionResult> ConsumeSession(Guid sessionId, CancellationToken ct)
    {
        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var command = new ConsumeVideoSessionCommand(sessionId, userId);

        var result = await _mediator.Send(command, ct);

        if (result.Success)
            return Ok(result);

        if (result.Errors != null && result.Errors.Contains("SESSION_NOT_FOUND")) return NotFound(result);
        if (result.Errors != null && result.Errors.Contains("SESSION_CONSUMED")) return BadRequest(result);
        if (result.Errors != null && result.Errors.Contains("SESSION_EXPIRED")) return BadRequest(result);

        return BadRequest(result);
    }

    [InternalTokenAuthorize("API_CALLBACK_SECRET", "AI_CALLBACK_SECRET")]
    [DisableRateLimiting]
    [HttpGet("{sessionId:guid}/embed-material")]
    [HttpGet("~/api/v1/internal/video-sessions/{sessionId:guid}/embed-material")]
    public async Task<IActionResult> GetEmbedMaterial(Guid sessionId,
        [FromServices] NaderGorge.Application.Services.VideoSessionMaterialService materialService,
        [FromServices] IAccessCheckService accessService,
        [FromQuery] bool includeWatermark,
        CancellationToken ct,
        [FromQuery] bool nativeHls = false)
    {
        Response.Headers.CacheControl = "no-store";
        var userIdClaim = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();
        var session = await _db.VideoPlaybackSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);

        if (session == null) return NotFound("Video session not found.");
        // Ownership stays indistinguishable from a missing session; only the owner sees its lifecycle state.
        if (session.IsSuperseded) return Conflict("Video session was replaced by another playback session.");
        if (session.ExpiresAt <= DateTime.UtcNow) return StatusCode(StatusCodes.Status410Gone, "Video session expired.");

        if (!await CanReadPlaybackMaterialAsync(session, accessService, ct)) return Forbid();
        string token;
        string? bunnyEmbedQuery;
        try
        {
            token = await materialService.GetTokenAsync(session, ct, nativeHls);
            bunnyEmbedQuery = await materialService.GetBunnyEmbedQueryAsync(session, ct);
        }
        catch (Exception error) when (error is InvalidOperationException or System.Security.Cryptography.CryptographicException or ArgumentException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Video playback configuration is unavailable.");
        }
        if (!includeWatermark)
        {
            return Ok(new VideoEmbedMaterialResponse(token, session.EncryptionKey, session.ExpiresAt, BunnyEmbedQuery: bunnyEmbedQuery));
        }

        var watermark = await _db.PlatformSettings.AsNoTracking()
            .Where(setting => setting.Key == "EnableWatermark" || setting.Key.StartsWith("Watermark"))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, ct);
        return Ok(new VideoEmbedMaterialResponse(token, session.EncryptionKey, session.ExpiresAt, watermark, session.UserId.ToString(), bunnyEmbedQuery));
    }

    private async Task<bool> CanReadPlaybackMaterialAsync(Domain.Entities.VideoPlaybackSession session, IAccessCheckService access, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(user => user.Id == session.UserId && user.IsActive && !user.IsDeleted, ct)) return false;
        if (!VideoPlaybackAuthorization.CanPreview(User)) return await access.HasAccessToVideoSessionAsync(session, ct);
        if (!User.IsInRole("Teacher") || User.IsInRole("Admin")) return true;
        var lessonId = await _db.LessonVideos.Where(video => video.Id == session.LessonVideoId)
            .Select(video => (Guid?)video.LessonId).SingleOrDefaultAsync(ct);
        var teacherAuthorization = new NaderGorge.Application.Services.TeacherAuthorizationService(_db);
        return lessonId.HasValue && await teacherAuthorization.GetWorkspaceAccessAsync(session.UserId, ct) is not null
            && await teacherAuthorization.CanAccessLessonAsync(session.UserId, lessonId.Value, ct);
    }

    [HttpPost("{lessonVideoId}/track-progress")]
    [EnableRateLimiting("video-progress")]
    public async Task<IActionResult> TrackProgress(Guid lessonVideoId, [FromBody] TrackProgressRequest request, CancellationToken ct)
    {
        if (VideoPlaybackAuthorization.CanPreview(User)) return NoContent();

        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();

        var command = new TrackWatchProgressCommand(
            lessonVideoId,
            userId,
            request.SessionId,
            request.ProgressSequence,
            request.SecondsWatched,
            request.PlaybackRate,
            request.TotalDurationSeconds,
            request.ProgressSegments?.Select(segment => new WatchProgressSegment(
                segment.ProgressSequence,
                segment.SecondsWatched,
                segment.PlaybackRate)).ToList()
        );
        var result = await _mediator.Send(command, ct);

        if (result.Success) return Ok(result);
        if (result.Errors?.Contains("SESSION_INVALID") == true) return NotFound(result);
        if (result.Errors?.Contains("SESSION_EXPIRED") == true) return Conflict(result);
        if (result.Errors?.Contains("SESSION_SUPERSEDED") == true) return Conflict(result);
        return BadRequest(result);
    }

    [HttpPost("{sessionId:guid}/client-event")]
    public async Task<IActionResult> ReportClientEvent(
        Guid sessionId,
        [FromBody] VideoPlaybackClientEventRequest request,
        CancellationToken ct)
    {
        var userIdString = User.FindFirst("id")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();

        if (!request.IsValid()) return BadRequest();

        var session = await _db.VideoPlaybackSessions
            .AsNoTracking()
            .Where(candidate => candidate.Id == sessionId && candidate.UserId == userId)
            .Select(candidate => new { candidate.Id, candidate.LessonVideoId })
            .SingleOrDefaultAsync(ct);
        if (session is null) return NotFound();

        _logger.LogWarning(
            "Video client player event. Provider={Provider} Event={Event} Phase={Phase} StatusCode={StatusCode} ElapsedMs={ElapsedMs} Online={Online} Visibility={Visibility} LessonVideoId={LessonVideoId} SessionId={SessionId}",
            request.Provider,
            request.Event,
            request.Phase,
            request.StatusCode,
            request.ElapsedMs,
            request.Online,
            request.Visibility,
            session.LessonVideoId,
            session.Id);

        return NoContent();
    }

    [HttpPost("{lessonVideoId}/request-extra")]
    [Authorize(Roles = "Student,Admin")]
    [Idempotent]
    public async Task<IActionResult> RequestExtraWatch(Guid lessonVideoId, [FromBody] CreateExtraWatchRequest request, CancellationToken ct)
    {
        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();

        var result = await _mediator.Send(new CreateExtraWatchRequestCommand(lessonVideoId, userId, request.Reason), ct);
        if (result.Success) return Ok(result);
        if (result.Errors?.Contains("REQUEST_LIMIT_REACHED") == true) return BadRequest(result);
        if (result.Errors?.Contains("VIDEO_NOT_FOUND") == true) return NotFound(result);
        return BadRequest(result);
    }

    [HttpGet("{lessonVideoId}/request-status")]
    [Authorize(Roles = "Student,Admin")]
    public async Task<IActionResult> GetExtraWatchStatus(Guid lessonVideoId, CancellationToken ct)
    {
        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();

        var result = await _mediator.Send(new CheckExtraWatchStatusQuery(lessonVideoId, userId), ct);
        return Ok(result);
    }

    private string? GetIpAddress()
    {
        if (Request.Headers.ContainsKey("X-Forwarded-For"))
            return Request.Headers["X-Forwarded-For"];

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

public class TrackProgressRequest
{
    public Guid SessionId { get; set; }
    public long ProgressSequence { get; set; }
    public double SecondsWatched { get; set; }
    public double PlaybackRate { get; set; } = 1;
    public int TotalDurationSeconds { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(30)]
    public List<TrackProgressSegmentRequest>? ProgressSegments { get; set; }
}

public class TrackProgressSegmentRequest
{
    public long ProgressSequence { get; set; }
    public double SecondsWatched { get; set; }
    public double PlaybackRate { get; set; } = 1;
}

public class CreateVideoSessionRequest
{
    public Guid LessonVideoId { get; set; }
}

public class CreateExtraWatchRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed partial class VideoPlaybackClientEventRequest
{
    public string Provider { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public int? ElapsedMs { get; set; }
    public bool? Online { get; set; }
    public string? Visibility { get; set; }

    public bool IsValid() =>
        ((string.Equals(Provider, "bunny-hls", StringComparison.Ordinal)
          && string.Equals(Event, "playback-error", StringComparison.Ordinal))
         || (string.Equals(Provider, "bunny", StringComparison.Ordinal)
          && string.Equals(Event, "bridge-timeout", StringComparison.Ordinal)))
        && StatusCode is >= 0 and <= 599
        && (ElapsedMs is null or >= 0 and <= 120000)
        && (Visibility is null or "visible" or "hidden")
        && !string.IsNullOrWhiteSpace(Phase)
        && Phase.Length <= 80
        && SafePhasePattern().IsMatch(Phase);

    [GeneratedRegex("^[A-Za-z0-9_.:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePhasePattern();
}

public record VideoEmbedMaterialResponse(string Token, string Key, DateTime ExpiresAt,
    Dictionary<string, string>? WatermarkSettings = null, string? StudentId = null, string? BunnyEmbedQuery = null);
