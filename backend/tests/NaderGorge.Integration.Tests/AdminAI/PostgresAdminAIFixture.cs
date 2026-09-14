using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Infrastructure.Data;
using Npgsql;

namespace NaderGorge.Integration.Tests.AdminAI;

/// <summary>
/// Creates a disposable PostgreSQL database for AdminAI tests. The fixture
/// deliberately has no InMemory fallback: missing PostgreSQL is a failed gate.
/// </summary>
public sealed class PostgresAdminAIFixture : IAsyncDisposable
{
    private readonly string _databaseName;
    private readonly string _administrativeConnectionString;

    static PostgresAdminAIFixture() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private PostgresAdminAIFixture(
        string databaseName,
        string administrativeConnectionString,
        string connectionString)
    {
        _databaseName = databaseName;
        _administrativeConnectionString = administrativeConnectionString;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public AppDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    public static async Task<PostgresAdminAIFixture> CreateAsync()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "AdminAI PostgreSQL tests require ConnectionStrings__DefaultConnection; InMemory is forbidden.");
        var source = new NpgsqlConnectionStringBuilder(configured);
        var databaseName = $"nader_gorge_admin_ai_{Guid.NewGuid():N}";
        var administrative = new NpgsqlConnectionStringBuilder(source.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };

        await using (var connection = new NpgsqlConnection(administrative.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)}";
            await command.ExecuteNonQueryAsync();
        }

        var isolated = new NpgsqlConnectionStringBuilder(source.ConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        return new PostgresAdminAIFixture(
            databaseName,
            administrative.ConnectionString,
            isolated.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
        await using var connection = new NpgsqlConnection(_administrativeConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {QuoteIdentifier(_databaseName)} WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";
}
