using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIDataProtector : ILiveSupportAIDataProtector
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _digestKey;

    public LiveSupportAIDataProtector(IConfiguration configuration)
    {
        var secret = configuration["AI_CALLBACK_SECRET"] ?? configuration["Security:AIDataProtectionKey"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("AI data protection requires a strong secret.");
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes($"encrypt:{secret}"));
        _digestKey = SHA256.HashData(Encoding.UTF8.GetBytes($"digest:{secret}"));
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_encryptionKey, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return [.. nonce, .. tag, .. ciphertext];
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedPayload)
    {
        if (protectedPayload.Length < 28) throw new CryptographicException("Protected AI payload is invalid.");
        var plaintext = new byte[protectedPayload.Length - 28];
        using var aes = new AesGcm(_encryptionKey, 16);
        aes.Decrypt(protectedPayload[..12], protectedPayload[28..], protectedPayload.Slice(12, 16), plaintext);
        return plaintext;
    }

    public string ComputeKeyedDigest(string purpose, ReadOnlySpan<byte> value)
    {
        using var hmac = new HMACSHA256(_digestKey);
        hmac.TransformBlock(Encoding.UTF8.GetBytes(purpose), 0, Encoding.UTF8.GetByteCount(purpose), null, 0);
        hmac.TransformFinalBlock(value.ToArray(), 0, value.Length);
        return Convert.ToHexString(hmac.Hash!).ToLowerInvariant();
    }
}
