namespace NaderGorge.Application.Common;

public static class VideoProviders
{
    public const string YouTube = "youtube";
    public const string Vk = "vk";
    public const string Bunny = "bunny";

    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        YouTube,
        Vk,
        Bunny
    };

    public static bool IsSupported(string? provider)
    {
        return !string.IsNullOrWhiteSpace(provider) && SupportedProviders.Contains(provider.Trim());
    }

    public static string Normalize(string provider)
    {
        return provider.Trim().Equals("YouTube", StringComparison.OrdinalIgnoreCase)
            ? YouTube
            : provider.Trim().ToLowerInvariant();
    }
}
