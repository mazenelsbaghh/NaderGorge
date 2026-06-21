using Microsoft.EntityFrameworkCore;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class PostgresLiveSupportFixture : IAsyncDisposable
{
    public PostgresLiveSupportFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("PostgreSQL integration tests require ConnectionStrings__DefaultConnection and never use EF InMemory.");
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
    }
    public string ConnectionString { get; }
    public AppDbContext Db { get; }
    public async Task ResetAsync() { await Db.Database.EnsureDeletedAsync(); await Db.Database.EnsureCreatedAsync(); }
    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
