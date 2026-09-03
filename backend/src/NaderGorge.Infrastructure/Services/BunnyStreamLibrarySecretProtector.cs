using System.Text;
using Microsoft.AspNetCore.DataProtection;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyStreamLibrarySecretProtector : IBunnyStreamLibrarySecretProtector, IBunnyHlsSecretProtector
{
    private const string Purpose = "Massar.BunnyStream.LibraryApiKey.v1";
    private const string HlsPurpose = "Massar.BunnyStream.HlsTokenKey.v1";
    private readonly IDataProtectionProvider _provider;

    public BunnyStreamLibrarySecretProtector(IDataProtectionProvider provider)
    {
        _provider = provider;
    }

    public byte[] Protect(Guid libraryId, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return ProtectorFor(libraryId).Protect(Encoding.UTF8.GetBytes(apiKey));
    }

    public string Unprotect(Guid libraryId, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.IsEmpty)
        {
            throw new InvalidOperationException("The Bunny Stream library API key is not configured.");
        }

        return Encoding.UTF8.GetString(ProtectorFor(libraryId).Unprotect(ciphertext.ToArray()));
    }

    private IDataProtector ProtectorFor(Guid libraryId) =>
        _provider.CreateProtector(Purpose, libraryId.ToString("N"));

    byte[] IBunnyHlsSecretProtector.Protect(Guid libraryId, string tokenKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenKey);
        return HlsProtectorFor(libraryId).Protect(Encoding.UTF8.GetBytes(tokenKey));
    }

    string IBunnyHlsSecretProtector.Unprotect(Guid libraryId, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.IsEmpty) throw new InvalidOperationException("Bunny HLS token key is not configured.");
        return Encoding.UTF8.GetString(HlsProtectorFor(libraryId).Unprotect(ciphertext.ToArray()));
    }

    private IDataProtector HlsProtectorFor(Guid libraryId) =>
        _provider.CreateProtector(HlsPurpose, libraryId.ToString("N"));
}
