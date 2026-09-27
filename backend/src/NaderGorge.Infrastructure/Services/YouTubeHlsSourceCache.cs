using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

namespace NaderGorge.Infrastructure.Services;

public sealed class YouTubeHlsSourceCache(IDistributedCache cache, IDataProtectionProvider protection)
{
    public const int MaxSourceBytes = 2 * 1024 * 1024;
    private const string KeyPrefix = "massar:youtube-hls-source:v1:";

    public static bool IsVersion(string version) => version.Length == 16
        && version.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public async Task StoreAsync(string videoId, JsonElement source, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(source.GetRawText());
        if (bytes.Length > MaxSourceBytes) throw new ArgumentException("YouTube HLS source exceeds the size limit.");
        var (version, expiresAt) = SourceIdentity(source, videoId);
        var expires = DateTimeOffset.FromUnixTimeMilliseconds(expiresAt);
        if (expires <= DateTimeOffset.UtcNow || expires >= DateTimeOffset.UtcNow.AddHours(24))
            throw new ArgumentException("YouTube HLS source expiry is invalid.");
        var options = new DistributedCacheEntryOptions { AbsoluteExpiration = expires };
        var encrypted = Protector(videoId, version).Protect(bytes);
        // A latest pointer is published only after another node can read the complete version.
        await cache.SetAsync(KeyPrefix + videoId + ":" + version, encrypted, options, ct);
        await cache.SetStringAsync(KeyPrefix + videoId + ":latest", version, options, ct);
    }

    public async Task<byte[]?> ReadAsync(string videoId, string? version, CancellationToken ct)
    {
        version ??= await cache.GetStringAsync(KeyPrefix + videoId + ":latest", ct);
        if (version is null) return null;
        if (!IsVersion(version)) throw new InvalidOperationException("YouTube HLS cache identity is invalid.");
        var encrypted = await cache.GetAsync(KeyPrefix + videoId + ":" + version, ct);
        if (encrypted is null) return null;
        var bytes = Protector(videoId, version).Unprotect(encrypted);
        if (bytes.Length > MaxSourceBytes) throw new InvalidOperationException("YouTube HLS cache size is invalid.");
        using var document = JsonDocument.Parse(bytes);
        var identity = SourceIdentity(document.RootElement, videoId);
        if (identity.Version != version) throw new InvalidOperationException("YouTube HLS cache version is invalid.");
        return identity.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ? bytes : null;
    }

    private static (string Version, long ExpiresAt) SourceIdentity(JsonElement source, string videoId)
    {
        if (source.ValueKind != JsonValueKind.Object
            || !source.TryGetProperty("videoId", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != videoId
            || !source.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String || !IsVersion(version.GetString()!)
            || !source.TryGetProperty("expiresAt", out var expiry) || expiry.ValueKind != JsonValueKind.Number
            || !expiry.TryGetInt64(out var expiresAt) || expiresAt <= 0)
            throw new ArgumentException("YouTube HLS source identity is invalid.");
        return (version.GetString()!, expiresAt);
    }

    private IDataProtector Protector(string videoId, string version) =>
        protection.CreateProtector("Massar.YouTubeHls.Source.v1", videoId, version);
}
