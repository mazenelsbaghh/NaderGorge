using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyStreamLegacyCredentialImporter(
    AppDbContext db,
    IBunnyStreamLibrarySecretProtector protector,
    IConfiguration configuration,
    ILogger<BunnyStreamLegacyCredentialImporter> logger)
{
    public async Task ImportAsync(CancellationToken cancellationToken = default)
    {
        var credential = ReadConfiguredCredential();
        if (credential is null) return;

        var library = await db.BunnyStreamLibraries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.ExternalLibraryId == credential.ExternalLibraryId,
                cancellationToken);
        if (library is null)
        {
            logger.LogWarning(
                "Legacy Bunny Stream credentials do not match a registered library; no credential was imported.");
            return;
        }

        if (library.ApiKeyCiphertext is { Length: > 0 }) return;
        await StoreEncryptedCredentialAsync(library.Id, credential.ApiKey, cancellationToken);
    }

    private LegacyBunnyStreamCredential? ReadConfiguredCredential()
    {
        var configuredLibraryId = configuration["BunnyStream:LibraryId"]?.Trim();
        var configuredApiKey = configuration["BunnyStream:ApiKey"]?.Trim();
        if (string.IsNullOrEmpty(configuredLibraryId) && string.IsNullOrEmpty(configuredApiKey))
            return null;

        if (!long.TryParse(
                configuredLibraryId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var externalLibraryId) ||
            externalLibraryId <= 0 ||
            string.IsNullOrEmpty(configuredApiKey) ||
            configuredApiKey.Length > 512)
        {
            logger.LogWarning(
                "Legacy Bunny Stream credentials are incomplete or invalid; no credential was imported.");
            return null;
        }

        return new LegacyBunnyStreamCredential(externalLibraryId, configuredApiKey);
    }

    private async Task StoreEncryptedCredentialAsync(
        Guid libraryId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var ciphertext = protector.Protect(libraryId, apiKey);
        var importedAt = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var imported = await db.BunnyStreamLibraries
            .Where(library => library.Id == libraryId && library.ApiKeyCiphertext == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(library => library.ApiKeyCiphertext, ciphertext)
                .SetProperty(library => library.UpdatedAt, importedAt),
                cancellationToken);
        if (imported == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "BunnyStreamLibrary.ImportLegacyApiKey",
            EntityType = nameof(BunnyStreamLibrary),
            EntityId = libraryId,
            ActorType = "System",
            NewValues = JsonSerializer.Serialize(new
            {
                ApiKeyConfigured = true,
                Source = "LegacyConfiguration"
            })
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Imported a legacy Bunny Stream credential into library record {LibraryRecordId}.",
            libraryId);
    }

    private sealed record LegacyBunnyStreamCredential(long ExternalLibraryId, string ApiKey);
}
