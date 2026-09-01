using System.Text;
using Microsoft.AspNetCore.DataProtection;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyStreamLibrarySecretProtector : IBunnyStreamLibrarySecretProtector
{
    private const string Purpose = "Massar.BunnyStream.LibraryApiKey.v1";
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
}
