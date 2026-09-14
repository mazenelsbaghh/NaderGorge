using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIDataProtectionTests
{
    [Fact]
    public void Protect_RoundTripsOnlyWithSamePurposeAndUntamperedDigest()
    {
        var service = Create();
        var value = service.Protect("proposal", Encoding.UTF8.GetBytes("safe-redacted-payload"));
        Assert.Equal("safe-redacted-payload", Encoding.UTF8.GetString(service.Unprotect("proposal", value)));
        Assert.ThrowsAny<Exception>(() => service.Unprotect("secure-input", value));
        Assert.Throws<CryptographicException>(() => service.Unprotect("proposal", value with { Digest = new string('0', 64) }));
    }

    [Fact]
    public void PurposeSeparation_ProducesDifferentDigests()
    {
        var service = Create(); var bytes = Encoding.UTF8.GetBytes("same");
        Assert.NotEqual(service.Digest("proposal", bytes), service.Digest("audit", bytes));
    }

    [Theory]
    [InlineData("  نفّذ   ABC-123  ", "نفّذ ABC-123")]
    [InlineData("A\tB\nC", "A B C")]
    public void PhraseNormalization_IsNfcTrimAndWhitespaceOnly(string input, string expected) =>
        Assert.Equal(expected, Create().NormalizeConfirmationPhrase(input));

    [Fact]
    public void MissingOrShortHmacKey_FailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => Create(null));
        Assert.Throws<InvalidOperationException>(() => Create(Convert.ToBase64String(new byte[8])));
    }

    private static AdminAIDataProtector Create(string? key = "default")
    {
        var value = key == "default" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : key;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            value is null ? [] : new Dictionary<string, string?> { ["AdminAI:HmacKey"] = value }).Build();
        return new AdminAIDataProtector(new EphemeralDataProtectionProvider(), configuration);
    }
}
