using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

public sealed class BunnyHlsUrlSignerTests
{
    [Fact]
    public void DirectoryPlaylist_UsesBunnyAdvancedTokenContract()
    {
        var signer = new BunnyHlsUrlSigner();
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(1598024587).UtcDateTime;

        var signedUrl = signer.SignPlaylist(
            "vz-example.b-cdn.net",
            "0702a4de-be07-4e5b-85fc-e01b6f3ef92a",
            "SecurityKey",
            expiresAt);

        Assert.Equal(
            "https://vz-example.b-cdn.net/bcdn_token=HS256-ki2X1kk13V2x-cnsDZRRc1t8a_KzQDmspva6C4D2k1o&expires=1598024587&token_path=%2F0702a4de-be07-4e5b-85fc-e01b6f3ef92a%2F/0702a4de-be07-4e5b-85fc-e01b6f3ef92a/playlist.m3u8",
            signedUrl);
    }
}
