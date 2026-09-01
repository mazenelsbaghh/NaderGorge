using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed record FacebookMessengerRuntimeConfiguration(
    string AppId,
    string VerifyToken,
    string AppSecret,
    string ApiVersion,
    bool IsEnabled,
    bool IsDatabaseManaged,
    IReadOnlyDictionary<string, FacebookMessengerPageConfiguration> Pages)
{
    public bool TryGetPage(string pageId, out FacebookMessengerPageConfiguration page)
    {
        if (IsEnabled && Pages.TryGetValue(pageId, out page!)) return true;
        page = null!;
        return false;
    }

    public FacebookMessengerPageConfiguration RequirePage(string pageId) =>
        TryGetPage(pageId, out var page)
            ? page
            : throw new FacebookMessengerConfigurationException("MESSENGER_PAGE_NOT_CONFIGURED");
}

public interface IFacebookMessengerRuntimeConfigurationReader
{
    Task<FacebookMessengerRuntimeConfiguration> GetAsync(CancellationToken ct = default);
}

public interface IFacebookMessengerSecretProtector
{
    byte[] Protect(Guid entityId, string secretKind, string plaintext);
    string Unprotect(Guid entityId, string secretKind, ReadOnlySpan<byte> ciphertext);
}

public sealed class FacebookMessengerSecretProtector(IDataProtectionProvider provider)
    : IFacebookMessengerSecretProtector
{
    private const string Purpose = "Massar.LiveSupport.FacebookMessenger.Secrets.v1";

    public byte[] Protect(Guid entityId, string secretKind, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return ProtectorFor(entityId, secretKind).Protect(Encoding.UTF8.GetBytes(plaintext));
    }

    public string Unprotect(Guid entityId, string secretKind, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.IsEmpty)
            throw new CryptographicException("The requested Messenger secret is not configured.");
        return Encoding.UTF8.GetString(
            ProtectorFor(entityId, secretKind).Unprotect(ciphertext.ToArray()));
    }

    private IDataProtector ProtectorFor(Guid entityId, string secretKind) =>
        provider.CreateProtector(Purpose, entityId.ToString("N"), secretKind);
}

public sealed class FacebookMessengerRuntimeConfigurationReader(
    IAppDbContext db,
    IFacebookMessengerSecretProtector protector,
    FacebookMessengerConfiguration environmentFallback)
    : IFacebookMessengerRuntimeConfigurationReader
{
    public async Task<FacebookMessengerRuntimeConfiguration> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.ConfigurationKey == LiveSupportMessengerConfiguration.DefaultConfigurationKey,
                ct);
        if (settings is null) return FromEnvironment(environmentFallback);

        try
        {
            if (!FacebookMessengerConfiguration.IsValidApiVersion(settings.ApiVersion))
                throw new FacebookMessengerConfigurationException("MESSENGER_API_VERSION_INVALID");

            var pages = await db.LiveSupportMessengerPages
                .AsNoTracking()
                .Where(page => page.IsEnabled && page.ConnectionStatus == "Connected")
                .OrderBy(page => page.CreatedAt)
                .Take(4)
                .ToListAsync(ct);
            if (pages.Count > 3)
                throw new FacebookMessengerConfigurationException("MESSENGER_PAGE_LIMIT_EXCEEDED");

            var runtimePages = pages.ToDictionary(
                page => page.PageId,
                page => new FacebookMessengerPageConfiguration(
                    page.PageId,
                    page.DisplayName,
                    protector.Unprotect(page.Id, "page-access-token", page.PageAccessTokenCiphertext),
                    page.HumanAgentEnabled),
                StringComparer.Ordinal);
            var verifyToken = OptionalSecret(
                settings.Id,
                "verify-token",
                settings.VerifyTokenCiphertext);
            var appSecret = OptionalSecret(
                settings.Id,
                "app-secret",
                settings.AppSecretCiphertext);
            var isEnabled = settings.IsEnabled &&
                !string.IsNullOrWhiteSpace(settings.AppId) &&
                verifyToken.Length > 0 &&
                appSecret.Length > 0 &&
                runtimePages.Count > 0;
            return new FacebookMessengerRuntimeConfiguration(
                settings.AppId,
                verifyToken,
                appSecret,
                settings.ApiVersion,
                isEnabled,
                true,
                runtimePages);
        }
        catch (FacebookMessengerConfigurationException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            throw new FacebookMessengerConfigurationException("MESSENGER_SECRET_DECRYPTION_FAILED");
        }
    }

    private string OptionalSecret(Guid entityId, string kind, byte[]? ciphertext) =>
        ciphertext is { Length: > 0 }
            ? protector.Unprotect(entityId, kind, ciphertext)
            : string.Empty;

    private static FacebookMessengerRuntimeConfiguration FromEnvironment(
        FacebookMessengerConfiguration configuration) =>
        new(
            string.Empty,
            configuration.VerifyToken,
            configuration.AppSecret,
            configuration.ApiVersion,
            configuration.Pages.Count > 0,
            false,
            configuration.Pages.ToDictionary(page => page.PageId, StringComparer.Ordinal));
}

public sealed class FixedFacebookMessengerRuntimeConfigurationReader(
    FacebookMessengerRuntimeConfiguration configuration)
    : IFacebookMessengerRuntimeConfigurationReader
{
    public Task<FacebookMessengerRuntimeConfiguration> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(configuration);

    public static FixedFacebookMessengerRuntimeConfigurationReader FromEnvironment(
        FacebookMessengerConfiguration configuration) =>
        new(new FacebookMessengerRuntimeConfiguration(
            string.Empty,
            configuration.VerifyToken,
            configuration.AppSecret,
            configuration.ApiVersion,
            configuration.Pages.Count > 0,
            false,
            configuration.Pages.ToDictionary(page => page.PageId, StringComparer.Ordinal)));
}
