using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class WhatsAppCampaignDataProtector : IWhatsAppCampaignDataProtector
{
    private const string ProtectionPurpose = "Massar.WhatsAppCampaigns.Recipients.v1";
    private readonly IDataProtector _protector;
    private readonly byte[] _hmacKey;

    public WhatsAppCampaignDataProtector(
        IDataProtectionProvider provider,
        IConfiguration configuration)
    {
        _protector = provider.CreateProtector(ProtectionPurpose);
        _hmacKey = ResolveHmacKey(configuration);
    }

    public byte[] Protect(Guid recipientId, ReadOnlySpan<byte> plaintext) =>
        _protector.CreateProtector(recipientId.ToString("N")).Protect(plaintext.ToArray());

    public byte[] Unprotect(Guid recipientId, ReadOnlySpan<byte> ciphertext, string digest)
    {
        var expected = Digest(recipientId, ciphertext);
        if (!FixedEquals(expected, digest))
            throw new CryptographicException("WhatsApp campaign payload integrity validation failed.");
        return _protector.CreateProtector(recipientId.ToString("N")).Unprotect(ciphertext.ToArray());
    }

    public string Digest(Guid recipientId, ReadOnlySpan<byte> ciphertext) =>
        Hmac($"payload:{recipientId:N}", ciphertext);

    public string DestinationHash(string e164Phone) =>
        Hmac("destination", Encoding.UTF8.GetBytes(e164Phone));

    public string SecretHash(string purpose, string value)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 80)
            throw new ArgumentException("A bounded purpose is required.", nameof(purpose));
        return Hmac($"secret:{purpose}", Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC)));
    }

    private string Hmac(string purpose, ReadOnlySpan<byte> value)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var purposeBytes = Encoding.UTF8.GetBytes(purpose);
        var payload = new byte[purposeBytes.Length + 1 + value.Length];
        purposeBytes.CopyTo(payload, 0);
        value.CopyTo(payload.AsSpan(purposeBytes.Length + 1));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    private static bool FixedEquals(string expected, string supplied)
    {
        if (expected.Length != supplied.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied));
    }

    private static byte[] ResolveHmacKey(IConfiguration configuration)
    {
        var configured = configuration["WhatsAppCampaigns:HmacKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var decoded = Convert.FromBase64String(configured);
            if (decoded.Length < 32)
                throw new InvalidOperationException("WhatsAppCampaigns:HmacKey must contain at least 256 bits.");
            return decoded;
        }

        var adminKey = configuration["AdminAI:HmacKey"];
        if (!string.IsNullOrWhiteSpace(adminKey))
        {
            var decoded = Convert.FromBase64String(adminKey);
            if (decoded.Length >= 32) return decoded;
        }

        throw new InvalidOperationException(
            "WhatsAppCampaigns:HmacKey or the stable AdminAI:HmacKey is required and must contain at least 256 bits.");
    }
}
