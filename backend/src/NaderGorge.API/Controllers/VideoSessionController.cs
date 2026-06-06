using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Configuration;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;

using Microsoft.AspNetCore.RateLimiting;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/student/video-session")]
[Authorize(Roles = "Student,Admin")]
[EnableRateLimiting("video-session")]
public class VideoSessionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IConfiguration _configuration;

    public VideoSessionController(IMediator mediator, IAppDbContext db, IConfiguration configuration)
    {
        _mediator = mediator;
        _db = db;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateVideoSessionRequest request, CancellationToken ct)
    {
        // Get user ID from claims (custom extension logic or fallback to generic)
        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var command = new CreateVideoSessionCommand(
            request.LessonVideoId,
            userId,
            GetIpAddress()
        );

        var result = await _mediator.Send(command, ct);

        if (result.Success)
            return Ok(result);

        // Map common errors
        if (result.Errors != null && result.Errors.Contains("VIDEO_NOT_FOUND")) return NotFound(result);
        if (result.Errors != null && result.Errors.Contains("ACCESS_DENIED")) return Forbid(); // Needs properly handling Result in API
        if (result.Errors != null && result.Errors.Contains("WATCH_LIMIT_REACHED")) return BadRequest(result);

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

    [AllowAnonymous]
    [HttpGet("{sessionId:guid}/embed-material")]
    public async Task<IActionResult> GetEmbedMaterial(Guid sessionId, CancellationToken ct)
    {
        var suppliedToken = Request.Headers["X-Internal-Token"].FirstOrDefault();
        if (!ServiceTokenValidator.IsValid(
                suppliedToken,
                _configuration["API_CALLBACK_SECRET"],
                _configuration["AI_CALLBACK_SECRET"]))
        {
            return Unauthorized("Invalid internal token.");
        }

        var session = await _db.VideoPlaybackSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsConsumed && s.ExpiresAt > DateTime.UtcNow, ct);

        if (session == null)
        {
            return NotFound("Video session not found or expired.");
        }

        return Ok(new VideoEmbedMaterialResponse(session.SessionToken, session.EncryptionKey));
    }

    [HttpPost("{lessonVideoId}/track-progress")]
    public async Task<IActionResult> TrackProgress(Guid lessonVideoId, [FromBody] TrackProgressRequest request, CancellationToken ct)
    {
        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();
        if (request.TotalDurationSeconds <= 0) return BadRequest(new { success = false, errors = new[] { "DURATION_REQUIRED" } });

        var command = new TrackWatchProgressCommand(
            lessonVideoId,
            userId,
            request.SecondsWatched,
            request.TotalDurationSeconds,
            request.RegisterView
        );
        var result = await _mediator.Send(command, ct);

        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    [HttpPost("{lessonVideoId}/request-extra")]
    public async Task<IActionResult> RequestExtraWatch(Guid lessonVideoId, CancellationToken ct)
    {
        var userIdString = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();

        var result = await _mediator.Send(new CreateExtraWatchRequestCommand(lessonVideoId, userId), ct);
        if (result.Success) return Ok(result);
        if (result.Errors?.Contains("REQUEST_LIMIT_REACHED") == true) return BadRequest(result);
        if (result.Errors?.Contains("VIDEO_NOT_FOUND") == true) return NotFound(result);
        return BadRequest(result);
    }

    [HttpGet("{lessonVideoId}/request-status")]
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
    public double SecondsWatched { get; set; }
    public int TotalDurationSeconds { get; set; }
    public bool RegisterView { get; set; }
}

public class CreateVideoSessionRequest
{
    public Guid LessonVideoId { get; set; }
}

public record VideoEmbedMaterialResponse(string Token, string Key);
