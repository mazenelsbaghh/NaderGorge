using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Student;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class VideoSessionMaterialService(
    IAppDbContext db,
    IVideoEncryptionService encryption,
    IBunnyHlsUrlSigner signer,
    IBunnyHlsSecretProtector protector,
    IBunnyPlayerTokenSigner? playerSigner = null)
{
    public async Task<string> GetTokenAsync(VideoPlaybackSession session, CancellationToken ct, bool nativeHls = false)
    {
        var material = encryption.DecryptVideoInfo(session.SessionToken, session.EncryptionKey);
        if (!string.Equals(material.ProviderName, "bunny-hls", StringComparison.OrdinalIgnoreCase))
            return session.SessionToken;

        var video = await db.LessonVideos.AsNoTracking()
            .Include(video => video.BunnyStreamLibrary)
            .SingleAsync(video => video.Id == session.LessonVideoId, ct);
        var library = video.BunnyStreamLibrary;
        var original = new Uri(material.ProviderVideoId);
        // A renewed session must not silently switch to a replacement video/library.
        if (library?.HlsTokenKeyCiphertext is not { Length: > 0 } ciphertext
            || !string.Equals(original.Host, library.HlsCdnHostname, StringComparison.OrdinalIgnoreCase)
            || !original.AbsolutePath.EndsWith($"/{video.ProviderVideoId}/playlist.m3u8", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("HLS session source is no longer available.");

        var signedUrl = signer.SignPlaylist(original.Host, video.ProviderVideoId,
            protector.Unprotect(library.Id, ciphertext), nativeHls
                ? session.ExpiresAt : VideoPlaybackSessionPolicy.MediaExpiresAt(session.ExpiresAt, DateTime.UtcNow));
        // Generate response material only: never overwrite concurrent watch progress.
        return encryption.EncryptVideoInfo(material.ProviderName, signedUrl, session.EncryptionKey,
            material.StudentName, material.StudentPhone);
    }

    public async Task<string?> GetBunnyEmbedQueryAsync(VideoPlaybackSession session, CancellationToken ct)
    {
        var material = encryption.DecryptVideoInfo(session.SessionToken, session.EncryptionKey);
        if (!string.Equals(material.ProviderName, "bunny", StringComparison.OrdinalIgnoreCase) || playerSigner is null)
            return null;
        var video = await db.LessonVideos.AsNoTracking().Include(video => video.BunnyStreamLibrary)
            .SingleAsync(video => video.Id == session.LessonVideoId, ct);
        var library = video.BunnyStreamLibrary;
        if (library is null || !string.Equals(material.ProviderVideoId,
                $"{library.ExternalLibraryId}/{video.ProviderVideoId}", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bunny session source is no longer available.");
        return playerSigner.SignQuery(library.ExternalLibraryId, video.ProviderVideoId, session.ExpiresAt);
    }
}
