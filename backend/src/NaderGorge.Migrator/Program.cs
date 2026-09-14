using Microsoft.EntityFrameworkCore;
using NaderGorge.Infrastructure.Data;
using Npgsql;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

const long MigrationLockKey = 4_832_779_884_013_771_991;

static string SafeExceptionSummary(Exception exception, string connectionString)
{
    var messages = new List<string>();
    for (var current = exception; current is not null; current = current.InnerException)
    {
        if (!string.IsNullOrWhiteSpace(current.Message))
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
        }
    }

    var summary = string.Join(" | ", messages);
    try
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            summary = summary.Replace(builder.Password, "[REDACTED]", StringComparison.Ordinal);
        }
    }
    catch (ArgumentException)
    {
        // The connection was already accepted by Npgsql; this is defense in depth.
    }

    return summary.Replace(connectionString, "[REDACTED_CONNECTION]", StringComparison.Ordinal);
}

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("migration blocked: database connection reference is missing");
    return 3;
}

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

var acquired = false;
try
{
    await using (var lockCommand = connection.CreateCommand())
    {
        lockCommand.CommandText = "SELECT pg_try_advisory_lock(@key)";
        lockCommand.Parameters.AddWithValue("key", MigrationLockKey);
        acquired = (bool)(await lockCommand.ExecuteScalarAsync() ?? false);
    }

    if (!acquired)
    {
        Console.Error.WriteLine("migration blocked: another migrator owns the production lock");
        return 5;
    }

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connection)
        .EnableSensitiveDataLogging(false)
        .Options;

    await using var database = new AppDbContext(options);
    var pending = (await database.Database.GetPendingMigrationsAsync()).ToArray();
    Console.WriteLine($"pending migrations: {pending.Length}");
    foreach (var migration in pending)
    {
        Console.WriteLine($"apply migration: {migration}");
    }

    await database.Database.MigrateAsync();

    var remaining = (await database.Database.GetPendingMigrationsAsync()).ToArray();
    if (remaining.Length != 0)
    {
        Console.Error.WriteLine($"migration verification failed: {remaining.Length} migrations remain");
        return 6;
    }

    var applied = (await database.Database.GetAppliedMigrationsAsync()).LastOrDefault() ?? "none";
    Console.WriteLine($"migration target verified: {applied}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"migration failed: {SafeExceptionSummary(exception, connectionString)}");
    return 6;
}
finally
{
    if (acquired)
    {
        await using var unlockCommand = connection.CreateCommand();
        unlockCommand.CommandText = "SELECT pg_advisory_unlock(@key)";
        unlockCommand.Parameters.AddWithValue("key", MigrationLockKey);
        await unlockCommand.ExecuteScalarAsync();
    }
}
