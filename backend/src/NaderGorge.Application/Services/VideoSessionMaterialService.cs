using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class VideoSessionMaterialService(
    IAppDbContext db,
    IVideoEncryptionService encryption,
    IBunnyHlsUrlSigner signer,
    IBunnyHlsSecretProtector protector)
{
    public async Task<string> GetTokenAsync(VideoPlaybackSession session, CancellationToken ct)
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
            protector.Unprotect(library.Id, ciphertext), session.ExpiresAt);
        // Generate response material only: never overwrite concurrent watch progress.
        return encryption.EncryptVideoInfo(material.ProviderName, signedUrl, session.EncryptionKey,
            material.StudentName, material.StudentPhone);
    }
}
