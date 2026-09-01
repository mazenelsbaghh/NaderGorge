using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class BunnyStreamLegacyCredentialImporterTests
{
    private const long LegacyExternalLibraryId = 740733;
    private const string LegacyApiKey = "legacy-bunny-api-key";

    [Fact]
    public async Task ProductionRegression20260901_ExactLegacyLibraryEncryptsCredentialOnce()
    {
        await using var database = await RelationalTestDatabase.CreateAsync();
        var library = await FindLegacyLibraryAsync(database.Db);
        var protector = new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider());
        var importer = CreateImporter(database.Db, protector, LegacyExternalLibraryId, LegacyApiKey);

        await importer.ImportAsync();
        var firstCiphertext = (await FindLegacyLibraryAsync(database.Db, asNoTracking: true)).ApiKeyCiphertext!;
        await importer.ImportAsync();

        var storedLibrary = await FindLegacyLibraryAsync(database.Db, asNoTracking: true);
        Assert.False(Encoding.UTF8.GetBytes(LegacyApiKey).SequenceEqual(storedLibrary.ApiKeyCiphertext!));
        Assert.Equal(LegacyApiKey, protector.Unprotect(library.Id, storedLibrary.ApiKeyCiphertext!));
        Assert.Equal(firstCiphertext, storedLibrary.ApiKeyCiphertext);
        var audit = Assert.Single(await database.Db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal("System", audit.ActorType);
        Assert.DoesNotContain(LegacyApiKey, audit.NewValues ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DifferentRegisteredLibrary_DoesNotReceiveLegacyCredential()
    {
        await using var database = await RelationalTestDatabase.CreateAsync();
        var protector = new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider());

        await CreateImporter(database.Db, protector, 999999, LegacyApiKey)
            .ImportAsync();

        Assert.All(
            await database.Db.BunnyStreamLibraries.AsNoTracking().ToListAsync(),
            library => Assert.Null(library.ApiKeyCiphertext));
        Assert.Empty(await database.Db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AdminConfiguredCredential_RemainsAuthoritativeOverLegacyEnvironment()
    {
        await using var database = await RelationalTestDatabase.CreateAsync();
        const string adminApiKey = "admin-configured-api-key";
        var protector = new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider());
        var library = await FindLegacyLibraryAsync(database.Db);
        library.ApiKeyCiphertext = protector.Protect(library.Id, adminApiKey);
        await database.Db.SaveChangesAsync();

        await CreateImporter(database.Db, protector, LegacyExternalLibraryId, LegacyApiKey)
            .ImportAsync();

        var storedLibrary = await FindLegacyLibraryAsync(database.Db, asNoTracking: true);
        Assert.Equal(adminApiKey, protector.Unprotect(library.Id, storedLibrary.ApiKeyCiphertext!));
        Assert.Empty(await database.Db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AdminUpdateBetweenReadAndWrite_PreventsLegacyCredentialOverwrite()
    {
        await using var database = await RelationalTestDatabase.CreateAsync();
        const string adminApiKey = "concurrent-admin-api-key";
        var protector = new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider());
        var adminCiphertext = protector.Protect(BunnyStreamLibrarySeedIds.First, adminApiKey);
        var racingProtector = new BeforeProtectSecretProtector(protector, () =>
            database.Db.BunnyStreamLibraries
                .Where(library => library.Id == BunnyStreamLibrarySeedIds.First)
                .ExecuteUpdate(setters => setters.SetProperty(
                    library => library.ApiKeyCiphertext,
                    adminCiphertext)));

        await CreateImporter(database.Db, racingProtector, LegacyExternalLibraryId, LegacyApiKey)
            .ImportAsync();

        var storedLibrary = await FindLegacyLibraryAsync(database.Db, asNoTracking: true);
        Assert.Equal(adminApiKey, protector.Unprotect(storedLibrary.Id, storedLibrary.ApiKeyCiphertext!));
        Assert.Empty(await database.Db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task MissingLegacyEnvironment_LeavesUnconfiguredLibraryUnchanged()
    {
        await using var database = await RelationalTestDatabase.CreateAsync();
        var protector = new BunnyStreamLibrarySecretProtector(new EphemeralDataProtectionProvider());
        var importer = CreateImporter(database.Db, protector, null, null);

        await importer.ImportAsync();

        var storedLibrary = await FindLegacyLibraryAsync(database.Db, asNoTracking: true);
        Assert.Null(storedLibrary.ApiKeyCiphertext);
        Assert.Empty(await database.Db.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task EncryptionFailure_PropagatesAndLeavesCredentialUnconfigured()
    {
        await using var database = await RelationalTestDatabase.CreateAsync();
        var importer = CreateImporter(
            database.Db,
            new FailingSecretProtector(),
            LegacyExternalLibraryId,
            LegacyApiKey);

        await Assert.ThrowsAsync<CryptographicException>(() => importer.ImportAsync());

        var storedLibrary = await FindLegacyLibraryAsync(database.Db, asNoTracking: true);
        Assert.Null(storedLibrary.ApiKeyCiphertext);
        Assert.Empty(await database.Db.AuditLogs.AsNoTracking().ToListAsync());
    }

    private static BunnyStreamLegacyCredentialImporter CreateImporter(
        AppDbContext db,
        IBunnyStreamLibrarySecretProtector protector,
        long? externalLibraryId,
        string? apiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BunnyStream:LibraryId"] = externalLibraryId?.ToString(),
                ["BunnyStream:ApiKey"] = apiKey
            })
            .Build();
        return new BunnyStreamLegacyCredentialImporter(
            db,
            protector,
            configuration,
            NullLogger<BunnyStreamLegacyCredentialImporter>.Instance);
    }

    private static Task<BunnyStreamLibrary> FindLegacyLibraryAsync(
        AppDbContext db,
        bool asNoTracking = false)
    {
        var libraries = asNoTracking
            ? db.BunnyStreamLibraries.AsNoTracking()
            : db.BunnyStreamLibraries;
        return libraries.SingleAsync(library =>
            library.ExternalLibraryId == LegacyExternalLibraryId);
    }

    private sealed class RelationalTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RelationalTestDatabase(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public AppDbContext Db { get; }

        public static async Task<RelationalTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new RelationalTestDatabase(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class BeforeProtectSecretProtector(
        IBunnyStreamLibrarySecretProtector inner,
        Action beforeProtect) : IBunnyStreamLibrarySecretProtector
    {
        public byte[] Protect(Guid libraryId, string apiKey)
        {
            beforeProtect();
            return inner.Protect(libraryId, apiKey);
        }

        public string Unprotect(Guid libraryId, ReadOnlySpan<byte> ciphertext) =>
            inner.Unprotect(libraryId, ciphertext);
    }

    private sealed class FailingSecretProtector : IBunnyStreamLibrarySecretProtector
    {
        public byte[] Protect(Guid libraryId, string apiKey) =>
            throw new CryptographicException("Simulated Data Protection failure.");

        public string Unprotect(Guid libraryId, ReadOnlySpan<byte> ciphertext) =>
            throw new NotSupportedException();
    }
}
