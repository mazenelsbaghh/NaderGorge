using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NaderGorge.Application.Features.LiveSupportAI.Services;

public static partial class LiveSupportAISafety
{
    private static readonly HashSet<string> ForbiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordHash", "token", "refreshToken", "secret", "verificationAnswer", "expectedAnswer", "paymentSecret"
    };

    public static string NormalizeText(string value) => Whitespace().Replace(value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant(), " ");
    public static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());

    public static string HmacSha256(string value, ReadOnlySpan<byte> key)
    {
        using var hmac = new HMACSHA256(key.ToArray());
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    public static string SerializeBounded(IReadOnlyDictionary<string, object?> values, int maxBytes = 16_384)
    {
        if (values.Keys.Any(ForbiddenKeys.Contains)) throw new ArgumentException("Sensitive key is not allowed.", nameof(values));
        var json = JsonSerializer.Serialize(values);
        if (Encoding.UTF8.GetByteCount(json) > maxBytes) throw new ArgumentException("Safe JSON exceeds its size limit.", nameof(values));
        return json;
    }

    public static bool IsForbiddenKey(string key) => ForbiddenKeys.Contains(key);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
