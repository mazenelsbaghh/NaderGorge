namespace NaderGorge.Application.Interfaces;

public interface IBunnyPlayerTokenSigner
{
    // Null preserves playback for libraries whose embed-token key is not configured.
    // Invalid configured keys must fail instead of falling back to unsigned playback.
    string? SignQuery(long externalLibraryId, string videoGuid, DateTime expiresAtUtc);
}
