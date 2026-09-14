using Microsoft.Extensions.Configuration;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

public sealed class BunnyPlayerTokenSignerTests
{
    private const string VideoGuid = "32d140e2-e4f4-4eec-9d53-20371e9be607";
    private static readonly DateTime Expiration = DateTimeOffset.FromUnixTimeSeconds(1623440202).UtcDateTime;

    [Fact]
    public void SignQuery_UsesTheSelectedLibraryPlayerKeyAndBunnyEmbedContract()
    {
        var signer = CreateSigner("""
            {"759":"4742a81b-bf15-42fe-8b1c-8fcb9024c550","760":"another-library-key"}
            """);

        var query = signer.SignQuery(759, VideoGuid, Expiration);

        // Public sample inputs from bunny.net/docs/stream/token-authentication.
        Assert.Equal("token=a8617f6df2e9b55b65ac7112138c70417766d80614bfe146d0d9bb2bd21fef87&expires=1623440202", query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"760\":\"another-library-key\"}")]
    public void SignQuery_PreservesLibrariesWithoutConfiguredPlayerKeys(string? configuredKeys)
    {
        var signer = CreateSigner(configuredKeys);

        Assert.Null(signer.SignQuery(759, VideoGuid, Expiration));
    }

    [Theory]
    [InlineData("{\"759\":\"secret-must-not-leak\"")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"759\":42}")]
    [InlineData("{\"759\":null}")]
    [InlineData("{\"759\":\"   \"}")]
    [InlineData("{\"759\":\"secret\\nkey\"}")]
    public void SignQuery_RejectsInvalidConfigurationWithoutLeakingIt(string configuredKeys)
    {
        var signer = CreateSigner(configuredKeys);

        var failure = Assert.Throws<InvalidOperationException>(() => signer.SignQuery(759, VideoGuid, Expiration));

        Assert.Equal("Bunny player token configuration is invalid.", failure.Message);
        Assert.Null(failure.InnerException);
    }

    private static BunnyPlayerTokenSigner CreateSigner(string? configuredKeys)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BunnyAnalysis:PlayerTokenSecurityKeysJson"] = configuredKeys,
            ["BunnyStream:ApiKey"] = "stream-key-must-not-be-used-for-embed-signing",
            ["BunnyAnalysis:CdnTokenSecurityKeysJson"] = "{\"759\":\"cdn-key-must-not-be-used\"}"
        }).Build();
        return new BunnyPlayerTokenSigner(configuration);
    }
}
