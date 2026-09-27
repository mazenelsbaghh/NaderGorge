using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.API.Configuration;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services;
using StackExchange.Redis;

namespace NaderGorge.API.Controllers;

public partial class VideoSessionController
{
    [InternalTokenAuthorize("API_CALLBACK_SECRET", "AI_CALLBACK_SECRET")]
    [DisableRateLimiting]
    [HttpGet("~/api/v1/internal/video-sessions/{sessionId:guid}/youtube-hls-source")]
    public async Task<IActionResult> GetYouTubeHlsSource(Guid sessionId, [FromQuery] string? v,
        [FromServices] VideoSessionMaterialService material, [FromServices] IAccessCheckService access,
        [FromServices] YouTubeHlsSourceCache cache, CancellationToken ct)
    {
        var (session, denied) = await ReadAuthorizedPlaybackSessionAsync(sessionId, access, ct);
        if (denied is not null) return denied;
        if (v is not null && !YouTubeHlsSourceCache.IsVersion(v)) return BadRequest("Invalid source version.");
        try
        {
            var videoId = await material.GetYouTubeHlsVideoIdAsync(session!, ct);
            if (videoId is null) return Forbid();
            var source = await cache.ReadAsync(videoId, v, ct);
            return source is null ? NotFound() : File(source, "application/json; charset=utf-8");
        }
        catch (Exception error) when (IsYouTubeHlsCacheFailure(error) || error is ArgumentException)
        {
            return StatusCode(503, "YouTube HLS source is unavailable.");
        }
    }

    [InternalTokenAuthorize("API_CALLBACK_SECRET", "AI_CALLBACK_SECRET")]
    [DisableRateLimiting]
    [RequestSizeLimit(YouTubeHlsSourceCache.MaxSourceBytes)]
    [HttpPut("~/api/v1/internal/video-sessions/{sessionId:guid}/youtube-hls-source")]
    public async Task<IActionResult> PutYouTubeHlsSource(Guid sessionId, [FromBody] JsonElement source,
        [FromServices] VideoSessionMaterialService material, [FromServices] IAccessCheckService access,
        [FromServices] YouTubeHlsSourceCache cache, CancellationToken ct)
    {
        var (session, denied) = await ReadAuthorizedPlaybackSessionAsync(sessionId, access, ct);
        if (denied is not null) return denied;
        try
        {
            var videoId = await material.GetYouTubeHlsVideoIdAsync(session!, ct);
            if (videoId is null) return Forbid();
            await cache.StoreAsync(videoId, source, ct);
            return NoContent();
        }
        catch (ArgumentException) { return BadRequest("Invalid YouTube HLS source."); }
        catch (Exception error) when (IsYouTubeHlsCacheFailure(error))
        {
            return StatusCode(503, "YouTube HLS source is unavailable.");
        }
    }

    private static bool IsYouTubeHlsCacheFailure(Exception error) =>
        error is RedisException or TimeoutException or IOException or CryptographicException or InvalidOperationException or JsonException;
}
