using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Data;
using Npgsql;

namespace NaderGorge.Infrastructure.Services;

public sealed class PostgresClusterLeaseService(AppDbContext database)
    : IClusterLeaseService
{
    public async Task<ClusterLeaseClaim?> TryAcquireAsync(
        string name,
        Guid ownerToken,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        ValidateRequest(name, ownerToken, lifetime);
        var expiresAt = DateTime.UtcNow.Add(lifetime);
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
            INSERT INTO cluster_leases
                ("Name", "OwnerToken", "FencingGeneration", "ExpiresAt", "RenewedAt")
            VALUES
                (@name, @owner, 1, @expires, NOW())
            ON CONFLICT ("Name") DO UPDATE
            SET
                "OwnerToken" = EXCLUDED."OwnerToken",
                "FencingGeneration" = CASE
                    WHEN cluster_leases."OwnerToken" = EXCLUDED."OwnerToken"
                        THEN cluster_leases."FencingGeneration"
                    ELSE cluster_leases."FencingGeneration" + 1
                END,
                "ExpiresAt" = EXCLUDED."ExpiresAt",
                "RenewedAt" = NOW()
            WHERE cluster_leases."ExpiresAt" <= NOW()
               OR cluster_leases."OwnerToken" = EXCLUDED."OwnerToken"
            RETURNING "FencingGeneration", "ExpiresAt";
            """;
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("owner", ownerToken);
            command.Parameters.AddWithValue("expires", expiresAt);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ClusterLeaseClaim(
                name,
                ownerToken,
                reader.GetInt64(0),
                reader.GetDateTime(1));
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> RenewAsync(
        ClusterLeaseClaim claim,
        TimeSpan lifetime,
        string? outcome,
        CancellationToken cancellationToken)
    {
        ValidateRequest(claim.Name, claim.OwnerToken, lifetime);
        var affected = await database.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE cluster_leases
            SET "ExpiresAt" = {{DateTime.UtcNow.Add(lifetime)}},
                "RenewedAt" = NOW(),
                "LastOutcome" = {{outcome}}
            WHERE "Name" = {{claim.Name}}
              AND "OwnerToken" = {{claim.OwnerToken}}
              AND "FencingGeneration" = {{claim.FencingGeneration}}
              AND "ExpiresAt" > NOW();
            """, cancellationToken);
        return affected == 1;
    }

    public async Task ReleaseAsync(
        ClusterLeaseClaim claim,
        string? outcome,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE cluster_leases
            SET "ExpiresAt" = NOW(),
                "RenewedAt" = NOW(),
                "LastOutcome" = {{outcome}}
            WHERE "Name" = {{claim.Name}}
              AND "OwnerToken" = {{claim.OwnerToken}}
              AND "FencingGeneration" = {{claim.FencingGeneration}};
            """, cancellationToken);
    }

    private static void ValidateRequest(
        string name,
        Guid ownerToken,
        TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160)
        {
            throw new ArgumentException("Lease name is required and must not exceed 160 characters.", nameof(name));
        }
        if (ownerToken == Guid.Empty)
        {
            throw new ArgumentException("Lease owner token is required.", nameof(ownerToken));
        }
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }
    }
}
