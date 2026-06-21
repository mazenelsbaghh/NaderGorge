using Microsoft.EntityFrameworkCore;
using NaderGorge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class PostgresLiveSupportFixture : IAsyncDisposable
{
    static PostgresLiveSupportFixture() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    public PostgresLiveSupportFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("PostgreSQL integration tests require ConnectionStrings__DefaultConnection and never use EF InMemory.");
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
    }
    public string ConnectionString { get; }
    public AppDbContext Db { get; }
    public async Task ResetAsync() { await Db.Database.EnsureDeletedAsync(); await Db.Database.MigrateAsync(); }
    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
