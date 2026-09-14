using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIDataProtector : IAdminAIDataProtector
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private readonly IDataProtectionProvider _provider;
    private readonly byte[] _hmacKey;

    public AdminAIDataProtector(IDataProtectionProvider provider, IConfiguration configuration)
    {
        _provider = provider;
        var encoded = configuration["AdminAI:HmacKey"];
        if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("AdminAI:HmacKey is required.");
        _hmacKey = Convert.FromBase64String(encoded);
        if (_hmacKey.Length < 32) throw new InvalidOperationException("AdminAI:HmacKey must contain at least 256 bits.");
    }

    public AdminAIProtectedValue Protect(string purpose, ReadOnlySpan<byte> plaintext)
    {
        var ciphertext = _provider.CreateProtector(Purpose(purpose)).Protect(plaintext.ToArray());
        return new AdminAIProtectedValue(ciphertext, Digest(purpose, ciphertext));
    }

    public byte[] Unprotect(string purpose, AdminAIProtectedValue value)
    {
        var expected = Digest(purpose, value.Ciphertext);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(value.Digest)))
            throw new CryptographicException("Protected AdminAI value failed integrity validation.");
        return _provider.CreateProtector(Purpose(purpose)).Unprotect(value.Ciphertext);
    }

    public string Digest(string purpose, ReadOnlySpan<byte> value)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var purposeBytes = Encoding.UTF8.GetBytes(Purpose(purpose));
        var payload = new byte[purposeBytes.Length + 1 + value.Length];
        purposeBytes.CopyTo(payload, 0); payload[purposeBytes.Length] = 0; value.CopyTo(payload.AsSpan(purposeBytes.Length + 1));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    public string NormalizeConfirmationPhrase(string value) =>
        Whitespace.Replace(value.Normalize(NormalizationForm.FormC).Trim(), " ");

    private static string Purpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 100) throw new ArgumentException("A bounded purpose is required.", nameof(purpose));
        return $"Massar.AdminAI.v1:{purpose}";
    }
}
