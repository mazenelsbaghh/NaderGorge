using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Providers;

public sealed class BunnyVideoProvider : IVideoProvider
{
    private readonly string _libraryId;

    public BunnyVideoProvider(IConfiguration configuration)
    {
        _libraryId = configuration["BunnyStream:LibraryId"] ?? string.Empty;
    }

    public string Name => "bunny";

    public string ExtractVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim();
        var playerMatch = Regex.Match(trimmed, @"mediadelivery\.net/embed/\d+/([a-f0-9-]{32,36})", RegexOptions.IgnoreCase);
        if (playerMatch.Success)
        {
            return playerMatch.Groups[1].Value;
        }

        var guidMatch = Regex.Match(trimmed, @"^[a-f0-9-]{32,36}$", RegexOptions.IgnoreCase);
        return guidMatch.Success ? trimmed : trimmed;
    }

    public string GetEmbedUrl(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(_libraryId))
        {
            return string.Empty;
        }

        return $"https://player.mediadelivery.net/embed/{_libraryId}/{videoId}";
    }
}
