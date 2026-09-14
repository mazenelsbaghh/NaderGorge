using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

/// <summary>
/// Worker-only media relay for AI analysis. This endpoint intentionally exposes
/// only a byte stream after validating the current analysis run; it never sends
/// a Bunny URL or any Bunny credential outside the backend.
/// </summary>
[ApiController]
[AllowAnonymous]
[DisableRateLimiting]
[Route("api/v1/internal/ai-media")]
public sealed class InternalAiMediaController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _libraries;
    private readonly IBunnyStreamClientFactory _clients;
    private readonly IBunnyOriginalMediaReader _originalMediaReader;

    public InternalAiMediaController(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients,
        IBunnyOriginalMediaReader originalMediaReader)
    {
        _db = db;
        _libraries = libraries;
        _clients = clients;
        _originalMediaReader = originalMediaReader;
    }

    [HttpGet("bunny/{lessonVideoId:guid}/runs/{generationRunId:guid}/original")]
    [InternalTokenAuthorize("AiMediaRelay:Secret")]
    public async Task<IActionResult> GetBunnyOriginal(
        Guid lessonVideoId,
        Guid generationRunId,
        CancellationToken cancellationToken)
    {
        var video = await _db.LessonVideos
            .AsNoTracking()
            .Where(candidate => candidate.Id == lessonVideoId)
            .Select(candidate => new
            {
                candidate.Provider,
                candidate.ProviderVideoId,
                candidate.BunnyStreamLibraryId,
                candidate.IsProcessingAI,
                candidate.CurrentAiAnalysisRunId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (video is null)
        {
            return NotFound(new { code = "BUNNY_ANALYSIS_VIDEO_NOT_FOUND" });
        }

        if (VideoProviders.Normalize(video.Provider) != VideoProviders.Bunny)
        {
            return UnprocessableEntity(new { code = "BUNNY_ANALYSIS_PROVIDER_INVALID" });
        }

        if (!video.IsProcessingAI || video.CurrentAiAnalysisRunId != generationRunId)
        {
            return Conflict(new { code = "BUNNY_ANALYSIS_RUN_STALE" });
        }

        if (!video.BunnyStreamLibraryId.HasValue)
        {
            return UnprocessableEntity(new { code = "BUNNY_ANALYSIS_LIBRARY_MISSING" });
        }

        if (!BunnyVideoReferenceParser.TryParse(video.ProviderVideoId, out var reference) || reference is null)
        {
            return UnprocessableEntity(new { code = "BUNNY_ANALYSIS_VIDEO_REFERENCE_INVALID" });
        }

        var library = await _libraries.ResolveAsync(
            video.BunnyStreamLibraryId.Value,
            requireActive: false,
            cancellationToken);
        if (!library.Success || library.Access is null)
        {
            return UnprocessableEntity(new { code = library.ErrorCode ?? "BUNNY_ANALYSIS_LIBRARY_UNAVAILABLE" });
        }

        if (reference.ExternalLibraryId.HasValue
            && reference.ExternalLibraryId.Value != library.Access.ExternalLibraryId)
        {
            return UnprocessableEntity(new { code = "BUNNY_ANALYSIS_LIBRARY_MISMATCH" });
        }

        try
        {
            var bunnyVideo = await _clients
                .Create(library.Access.ExternalLibraryId, library.Access.ApiKey)
                .GetVideoAsync(reference.VideoGuid, cancellationToken);
            if (bunnyVideo is null)
            {
                return NotFound(new { code = "BUNNY_ANALYSIS_VIDEO_NOT_FOUND" });
            }

            // Bunny's documented completed status is 4. Do not trust a stale local
            // status cache for an operation that reads the original upload.
            if (bunnyVideo.Status != 4)
            {
                return UnprocessableEntity(new { code = "BUNNY_ANALYSIS_VIDEO_NOT_READY" });
            }

            if (!bunnyVideo.HasOriginal)
            {
                return UnprocessableEntity(new { code = "BUNNY_ANALYSIS_ORIGINAL_UNAVAILABLE" });
            }

            await using var media = await _originalMediaReader.OpenAsync(
                library.Access,
                reference.VideoGuid,
                cancellationToken);

            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = media.ContentType;
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.Pragma = "no-cache";
            if (media.ContentLength.HasValue)
            {
                Response.ContentLength = media.ContentLength.Value;
            }

            await media.Content.CopyToAsync(Response.Body, cancellationToken);
            return new EmptyResult();
        }
        catch (BunnyOriginalMediaException exception)
        {
            return StatusCode(exception.StatusCode, new { code = exception.ErrorCode });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "BUNNY_ANALYSIS_PROVIDER_UNAVAILABLE" });
        }
    }
}
