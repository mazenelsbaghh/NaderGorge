using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyPlayerTokenSigner(IConfiguration configuration) : IBunnyPlayerTokenSigner
{
    // This existing per-library setting also secures the internal play-data reader.
    // The Stream API key and CDN token key are separate credentials.
    private const string PlayerTokenConfigurationKey = "BunnyAnalysis:PlayerTokenSecurityKeysJson";
    private const string InvalidConfigurationMessage = "Bunny player token configuration is invalid.";

    public string? SignQuery(long externalLibraryId, string videoGuid, DateTime expiresAtUtc)
    {
        if (externalLibraryId <= 0) throw new ArgumentOutOfRangeException(nameof(externalLibraryId));
        if (!Guid.TryParseExact(videoGuid, "D", out _))
            throw new ArgumentException("Invalid Bunny video GUID.", nameof(videoGuid));

        var tokenKey = ResolvePlayerTokenKey(externalLibraryId);
        if (tokenKey is null) return null;

        var expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc))
            .ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = SHA256.HashData(Encoding.UTF8.GetBytes(tokenKey + videoGuid + expires));
        return $"token={Convert.ToHexString(signature).ToLowerInvariant()}&expires={expires}";
    }

    private string? ResolvePlayerTokenKey(long externalLibraryId)
    {
        var configuredMap = configuration[PlayerTokenConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredMap)) return null;

        var keysByLibrary = ParseConfiguredKeys(configuredMap);
        if (!keysByLibrary.TryGetValue(externalLibraryId.ToString(CultureInfo.InvariantCulture), out var tokenKey))
            return null;

        if (string.IsNullOrWhiteSpace(tokenKey) || tokenKey.Length > 512 || tokenKey.Any(char.IsControl))
            throw new InvalidOperationException(InvalidConfigurationMessage);

        return tokenKey.Trim();
    }

    private static Dictionary<string, string?> ParseConfiguredKeys(string configuredMap)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(configuredMap)
                ?? throw new InvalidOperationException(InvalidConfigurationMessage);
        }
        catch (JsonException)
        {
            // JSON parser messages can include secret values or configuration fragments.
            throw new InvalidOperationException(InvalidConfigurationMessage);
        }
    }
}
