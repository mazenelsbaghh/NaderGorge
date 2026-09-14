using System.Security.Cryptography;
using System.Text;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyHlsUrlSigner : IBunnyHlsUrlSigner
{
    public string SignPlaylist(string hostname, string videoGuid, string tokenKey, DateTime expiresAtUtc)
    {
        if (!Guid.TryParse(videoGuid, out var parsedGuid)) throw new ArgumentException("Invalid Bunny video GUID.", nameof(videoGuid));
        var safeHost = hostname.Trim().TrimEnd('/').ToLowerInvariant();
        if (safeHost.StartsWith("http", StringComparison.OrdinalIgnoreCase) || !safeHost.EndsWith(".b-cdn.net", StringComparison.Ordinal))
            throw new ArgumentException("Invalid Bunny CDN hostname.", nameof(hostname));

        var guid = parsedGuid.ToString("D");
        var tokenPath = $"/{guid}/";
        var expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(tokenKey),
            Encoding.UTF8.GetBytes(tokenPath + expires + "token_path=" + tokenPath));
        var token = "HS256-" + Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var encodedPath = Uri.EscapeDataString(tokenPath);
        return $"https://{safeHost}/bcdn_token={token}&expires={expires}&token_path={encodedPath}{tokenPath}playlist.m3u8";
    }
}
