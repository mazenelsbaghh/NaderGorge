using System.Text.RegularExpressions;

namespace NaderGorge.Application.Common;

public static class VcdnVideoReference
{
    public static string? ExtractVideoId(string source)
    {
        var candidate = source.Trim();
        if (candidate.StartsWith("<iframe", StringComparison.OrdinalIgnoreCase))
            candidate = Regex.Match(candidate, "\\bsrc\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase).Groups[1].Value;
        if (IsVideoId(candidate)) return candidate;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var url)
            || url.Scheme != "https" || !url.IsDefaultPort || url.UserInfo.Length > 0
            || url.Query.Length > 0 || url.Fragment.Length > 0) return null;
        var path = url.AbsolutePath[1..];
        var videoId = url.Host.ToLowerInvariant() switch
        {
            "embed.vcdn.me" => path.StartsWith("embed/", StringComparison.Ordinal) ? path[6..] : path,
            "stream.vcdn.me" when path.EndsWith("/master.m3u8", StringComparison.Ordinal)
                => path[..^"/master.m3u8".Length],
            _ => string.Empty
        };
        return IsVideoId(videoId) ? videoId : null;
    }

    private static bool IsVideoId(string videoId) => Guid.TryParseExact(videoId, "D", out _);
}
