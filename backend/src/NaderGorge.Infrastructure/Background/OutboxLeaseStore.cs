using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Background;

public sealed class OutboxLeaseStore
{
    private readonly DbContext _database;

    public OutboxLeaseStore(IAppDbContext database)
    {
        _database = database as DbContext
            ?? throw new InvalidOperationException(
                "Outbox lease storage requires an Entity Framework DbContext.");
    }

    public async Task<List<OutboxEvent>> ClaimBatchAsync(
        string workerId,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        try
        {
            var claimedEvents = await _database.Set<OutboxEvent>()
                .FromSqlInterpolated($"""
                    SELECT * FROM outbox_events
                    WHERE "ProcessedAt" IS NULL
                      AND "IsDeadLetter" = FALSE
                      AND "RetryCount" < 5
                      AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())
                      AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" <= NOW())
                    ORDER BY "CreatedAt", "Id"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);
            var claimedAt = await DatabaseUtcNowAsync(cancellationToken);
            AssignLease(claimedEvents, workerId, claimedAt, leaseDuration);

            await _database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return claimedEvents;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> TryAcknowledgeAsync(
        Guid eventId,
        string workerId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var affected = await _database.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE outbox_events
            SET "ProcessedAt" = NOW(),
                "ClaimedBy" = NULL,
                "ClaimedAt" = NULL,
                "LeaseExpiresAt" = NULL,
                "NextAttemptAt" = NULL,
                "PayloadJson" = {{payloadJson}}
            WHERE "Id" = {{eventId}}
              AND "ProcessedAt" IS NULL
              AND "ClaimedBy" = {{workerId}}
              AND "LeaseExpiresAt" > NOW()
            """, cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryRenewLeaseAsync(
        Guid eventId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var affected = _database.Database.IsNpgsql()
            ? await RenewPostgresLeaseAsync(
                eventId,
                workerId,
                leaseDuration,
                cancellationToken)
            : await RenewSqliteLeaseAsync(
                eventId,
                workerId,
                leaseDuration,
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryRecordFailureAsync(
        OutboxEvent failedEvent,
        string workerId,
        CancellationToken cancellationToken)
    {
        var affected = await _database.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE outbox_events
            SET "RetryCount" = {{failedEvent.RetryCount}},
                "UpdatedAt" = {{failedEvent.UpdatedAt}},
                "LastError" = {{failedEvent.LastError}},
                "IsDeadLetter" = {{failedEvent.IsDeadLetter}},
                "NextAttemptAt" = {{failedEvent.NextAttemptAt}},
                "ClaimedBy" = NULL,
                "ClaimedAt" = NULL,
                "LeaseExpiresAt" = NULL,
                "PayloadJson" = {{failedEvent.PayloadJson}}
            WHERE "Id" = {{failedEvent.Id}}
              AND "ProcessedAt" IS NULL
              AND "ClaimedBy" = {{workerId}}
              AND "LeaseExpiresAt" > NOW()
            """, cancellationToken);
        return affected == 1;
    }

    private Task<int> RenewPostgresLeaseAsync(
        Guid eventId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken) =>
        _database.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE outbox_events
            SET "LeaseExpiresAt" =
                    NOW() + make_interval(secs => {{leaseDuration.TotalSeconds}})
            WHERE "Id" = {{eventId}}
              AND "ProcessedAt" IS NULL
              AND "ClaimedBy" = {{workerId}}
              AND "LeaseExpiresAt" > NOW()
            """, cancellationToken);

    private Task<int> RenewSqliteLeaseAsync(
        Guid eventId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var leaseModifier = $"+{leaseDuration.TotalSeconds} seconds";
        return _database.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE outbox_events
            SET "LeaseExpiresAt" = datetime(CURRENT_TIMESTAMP, {{leaseModifier}})
            WHERE "Id" = {{eventId}}
              AND "ProcessedAt" IS NULL
              AND "ClaimedBy" = {{workerId}}
              AND "LeaseExpiresAt" > CURRENT_TIMESTAMP
            """, cancellationToken);
    }

    private Task<DateTime> DatabaseUtcNowAsync(CancellationToken cancellationToken) =>
        _database.Database
            .SqlQuery<DateTime>($"SELECT NOW() AS \"Value\"")
            .SingleAsync(cancellationToken);

    private static void AssignLease(
        IEnumerable<OutboxEvent> claimedEvents,
        string workerId,
        DateTime claimedAt,
        TimeSpan leaseDuration)
    {
        foreach (var outboxEvent in claimedEvents)
        {
            outboxEvent.ClaimedBy = workerId;
            outboxEvent.ClaimedAt = claimedAt;
            outboxEvent.LeaseExpiresAt = claimedAt + leaseDuration;
        }
    }
}
