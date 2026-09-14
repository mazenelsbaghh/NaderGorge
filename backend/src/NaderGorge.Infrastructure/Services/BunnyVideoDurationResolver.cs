using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyVideoDurationResolver : IBunnyVideoDurationResolver
{
    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MissCacheDuration = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;
    private readonly IBunnyStreamLibraryAccessService _libraryAccess;
    private readonly IBunnyStreamClientFactory _clientFactory;
    private readonly ILogger<BunnyVideoDurationResolver> _logger;

    public BunnyVideoDurationResolver(
        IMemoryCache cache,
        IBunnyStreamLibraryAccessService libraryAccess,
        IBunnyStreamClientFactory clientFactory,
        ILogger<BunnyVideoDurationResolver> logger)
    {
        _cache = cache;
        _libraryAccess = libraryAccess;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<int?> ResolveAsync(
        Guid libraryId,
        string videoGuid,
        CancellationToken cancellationToken)
    {
        if (libraryId == Guid.Empty || string.IsNullOrWhiteSpace(videoGuid))
            return null;

        var normalizedVideoGuid = videoGuid.Trim().ToLowerInvariant();
        var cacheKey = $"bunny-video-duration:v1:{libraryId:N}:{normalizedVideoGuid}";
        var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            var duration = await FetchAsync(libraryId, normalizedVideoGuid, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = duration is > 0
                ? SuccessCacheDuration
                : MissCacheDuration;
            return new DurationLookup(duration);
        });

        return cached?.DurationSeconds;
    }

    private async Task<int?> FetchAsync(
        Guid libraryId,
        string videoGuid,
        CancellationToken cancellationToken)
    {
        var libraryResult = await _libraryAccess.ResolveAsync(
            libraryId,
            requireActive: false,
            cancellationToken);
        if (!libraryResult.Success || libraryResult.Access is null)
            return null;

        try
        {
            var access = libraryResult.Access;
            var client = _clientFactory.Create(access.ExternalLibraryId, access.ApiKey);
            var video = await client.GetVideoAsync(videoGuid, cancellationToken);
            return video is { Length: > 0 }
                   && video.VideoLibraryId == access.ExternalLibraryId
                   && string.Equals(video.Guid, videoGuid, StringComparison.OrdinalIgnoreCase)
                ? video.Length
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
                                          or InvalidOperationException
                                          or System.Text.Json.JsonException
                                          or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Could not resolve Bunny duration for video {VideoGuid} in library {LibraryId}",
                videoGuid,
                libraryId);
            return null;
        }
    }

    private sealed record DurationLookup(int? DurationSeconds);
}
