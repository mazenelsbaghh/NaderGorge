namespace NaderGorge.Application.Common;

public static class VideoProviders
{
    public const string YouTube = "youtube";
    public const string YouTubeHls = "youtube-hls";
    public const string Vk = "vk";
    public const string Bunny = "bunny";
    public const string Vcdn = "vcdn";

    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        YouTube,
        Vk,
        Bunny,
        Vcdn
    };

    public static bool IsSupported(string? provider)
    {
        return !string.IsNullOrWhiteSpace(provider) && SupportedProviders.Contains(provider.Trim());
    }

    public static bool IsYouTubeVideoId(string videoId) => videoId.Length == 11
        && videoId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static string Normalize(string provider)
    {
        return provider.Trim().Equals("YouTube", StringComparison.OrdinalIgnoreCase)
            ? YouTube
            : provider.Trim().ToLowerInvariant();
    }
}
