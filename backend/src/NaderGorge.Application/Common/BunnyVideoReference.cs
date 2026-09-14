using System.Globalization;

namespace NaderGorge.Application.Common;

public sealed record BunnyVideoReference(long? ExternalLibraryId, string VideoGuid);

public static class BunnyVideoReferenceParser
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "player.mediadelivery.net",
        "iframe.mediadelivery.net"
    };

    public static bool TryParse(string? value, out BunnyVideoReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (Guid.TryParseExact(trimmed, "D", out var standaloneGuid))
        {
            reference = new BunnyVideoReference(null, standaloneGuid.ToString("D"));
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !AllowedHosts.Contains(uri.Host))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 3
            || !string.Equals(segments[0], "play", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(segments[0], "embed", StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var libraryId)
            || libraryId <= 0
            || !Guid.TryParseExact(segments[2], "D", out var videoGuid))
        {
            return false;
        }

        reference = new BunnyVideoReference(libraryId, videoGuid.ToString("D"));
        return true;
    }
}
