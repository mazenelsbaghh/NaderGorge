using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.HR;

public sealed class PostgresHrFixture : IAsyncDisposable
{
    static PostgresHrFixture() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    public PostgresHrFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("PostgreSQL HR tests require ConnectionStrings__DefaultConnection and never use EF InMemory.");
        Db = CreateContext();
    }

    public string ConnectionString { get; }
    public AppDbContext Db { get; }

    public AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    public async Task ResetAsync()
    {
        Npgsql.NpgsqlConnection.ClearAllPools();
        await Db.Database.EnsureDeletedAsync();
        await Db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
