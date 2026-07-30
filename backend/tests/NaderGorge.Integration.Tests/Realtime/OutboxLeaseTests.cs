using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Background;
using NaderGorge.Infrastructure.Data;
using Npgsql;

namespace NaderGorge.Integration.Tests.Realtime;

public sealed class OutboxLeaseTests : IAsyncLifetime
{
    private readonly string _baseConnectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException(
            "Outbox lease tests require ConnectionStrings__DefaultConnection.");
    private readonly string _schemaName = $"outbox_lease_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var createSchema = connection.CreateCommand();
        createSchema.CommandText = $"""CREATE SCHEMA "{_schemaName}";""";
        await createSchema.ExecuteNonQueryAsync();
        var isolatedConnection = new NpgsqlConnectionStringBuilder(
            _baseConnectionString)
        {
            SearchPath = _schemaName
        };
        _connectionString = isolatedConnection.ConnectionString;
        await using var database = CreateContext();
        await database.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var dropSchema = connection.CreateCommand();
        dropSchema.CommandText = $"""DROP SCHEMA IF EXISTS "{_schemaName}" CASCADE;""";
        await dropSchema.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task TwoWorkers_ClaimEachEventAtMostOnce()
    {
        var outboxEvent = await SeedEventAsync();
        var firstWorker = $"node-a:{Guid.NewGuid():N}";
        var secondWorker = $"node-b:{Guid.NewGuid():N}";

        var claims = await Task.WhenAll(
            ClaimAsync(firstWorker),
            ClaimAsync(secondWorker));

        Assert.Single(claims.SelectMany(claim => claim));
        Assert.Equal(
            outboxEvent.Id,
            claims.SelectMany(claim => claim).Single().Id);
    }

    [Fact]
    public async Task ExpiredLease_IsReclaimedAndOnlyCurrentOwnerCanAcknowledge()
    {
        var outboxEvent = await SeedEventAsync();
        var expiredWorker = $"node-a:{Guid.NewGuid():N}";
        var currentWorker = $"node-b:{Guid.NewGuid():N}";
        Assert.Single(await ClaimAsync(expiredWorker));
        await ExpireLeaseAsync(outboxEvent.Id);
        Assert.Single(await ClaimAsync(currentWorker));

        await using var staleDatabase = CreateContext();
        var staleStore = new OutboxLeaseStore(staleDatabase);
        Assert.False(await staleStore.TryAcknowledgeAsync(
            outboxEvent.Id,
            expiredWorker,
            outboxEvent.PayloadJson,
            CancellationToken.None));

        await using var currentDatabase = CreateContext();
        var currentStore = new OutboxLeaseStore(currentDatabase);
        Assert.True(await currentStore.TryAcknowledgeAsync(
            outboxEvent.Id,
            currentWorker,
            outboxEvent.PayloadJson,
            CancellationToken.None));
    }

    [Fact]
    public async Task WorkerCrash_LeavesClaimUnavailableUntilLeaseExpiry()
    {
        var outboxEvent = await SeedEventAsync();
        Assert.Single(await ClaimAsync($"crashed:{Guid.NewGuid():N}"));

        Assert.Empty(await ClaimAsync($"standby:{Guid.NewGuid():N}"));
        await ExpireLeaseAsync(outboxEvent.Id);
        Assert.Single(await ClaimAsync($"standby:{Guid.NewGuid():N}"));
    }

    [Fact]
    public async Task SlowDispatch_RenewalPreventsConcurrentReclaim()
    {
        var outboxEvent = await SeedEventAsync();
        var activeWorker = $"active:{Guid.NewGuid():N}";
        var standbyWorker = $"standby:{Guid.NewGuid():N}";
        var leaseDuration = TimeSpan.FromSeconds(2);
        await using var activeDatabase = CreateContext();
        var activeStore = new OutboxLeaseStore(activeDatabase);
        Assert.Single(await activeStore.ClaimBatchAsync(
            activeWorker,
            leaseDuration,
            batchSize: 1,
            CancellationToken.None));

        for (var renewalCount = 0; renewalCount < 5; renewalCount++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.True(await activeStore.TryRenewLeaseAsync(
                outboxEvent.Id,
                activeWorker,
                leaseDuration,
                CancellationToken.None));
        }

        Assert.Empty(await ClaimAsync(standbyWorker));
        Assert.True(await activeStore.TryAcknowledgeAsync(
            outboxEvent.Id,
            activeWorker,
            outboxEvent.PayloadJson,
            CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredOwner_CannotRenewOrRecordFailure()
    {
        var outboxEvent = await SeedEventAsync();
        var expiredWorker = $"expired:{Guid.NewGuid():N}";
        var claimedEvent = Assert.Single(await ClaimAsync(expiredWorker));
        await ExpireLeaseAsync(outboxEvent.Id);
        OutboxProcessorBackgroundService.RecordDispatchFailure(
            claimedEvent,
            new InvalidOperationException("late failure"),
            new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await using var database = CreateContext();
        var store = new OutboxLeaseStore(database);
        Assert.False(await store.TryRenewLeaseAsync(
            outboxEvent.Id,
            expiredWorker,
            TimeSpan.FromMinutes(2),
            CancellationToken.None));
        Assert.False(await store.TryRecordFailureAsync(
            claimedEvent,
            expiredWorker,
            CancellationToken.None));
    }

    [Fact]
    public async Task RetrySchedule_BlocksClaimUntilNextAttemptAt()
    {
        var outboxEvent = await SeedEventAsync(
            nextAttemptAt: new DateTime(
                2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var worker = $"retry:{Guid.NewGuid():N}";
        Assert.Empty(await ClaimAsync(worker));

        await using (var database = CreateContext())
        {
            await database.OutboxEvents
                .Where(candidate => candidate.Id == outboxEvent.Id)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(
                        candidate => candidate.NextAttemptAt,
                        new DateTime(
                            2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        Assert.Single(await ClaimAsync(worker));
    }

    [Fact]
    public async Task FifthFailure_DeadLettersEventAndPreventsAnotherClaim()
    {
        var outboxEvent = await SeedEventAsync(retryCount: 4);
        var worker = $"failure:{Guid.NewGuid():N}";
        var claimedEvent = Assert.Single(await ClaimAsync(worker));
        OutboxProcessorBackgroundService.RecordDispatchFailure(
            claimedEvent,
            new InvalidOperationException("integration failure"),
            DateTime.UtcNow);
        await using (var database = CreateContext())
        {
            var store = new OutboxLeaseStore(database);
            Assert.True(await store.TryRecordFailureAsync(
                claimedEvent,
                worker,
                CancellationToken.None));
        }

        await using var verification = CreateContext();
        var persisted = await verification.OutboxEvents
            .SingleAsync(candidate => candidate.Id == outboxEvent.Id);
        Assert.True(persisted.IsDeadLetter);
        Assert.Equal(5, persisted.RetryCount);
        Assert.Null(persisted.NextAttemptAt);
        Assert.Empty(await ClaimAsync($"standby:{Guid.NewGuid():N}"));
    }

    private async Task<OutboxEvent> SeedEventAsync(
        int retryCount = 0,
        DateTime? nextAttemptAt = null)
    {
        var outboxEvent = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "OutboxLeaseIntegration",
            PayloadJson = """{"message":"lease-test"}""",
            TargetGroup = "integration-outbox",
            CreatedAt = DateTime.UtcNow,
            RetryCount = retryCount,
            NextAttemptAt = nextAttemptAt
        };
        await using var database = CreateContext();
        database.OutboxEvents.Add(outboxEvent);
        await database.SaveChangesAsync();
        return outboxEvent;
    }

    private async Task<List<OutboxEvent>> ClaimAsync(string workerId)
    {
        await using var database = CreateContext();
        var store = new OutboxLeaseStore(database);
        return await store.ClaimBatchAsync(
            workerId,
            TimeSpan.FromMinutes(2),
            batchSize: 1,
            CancellationToken.None);
    }

    private async Task ExpireLeaseAsync(Guid eventId)
    {
        await using var database = CreateContext();
        await database.OutboxEvents
            .Where(outboxEvent => outboxEvent.Id == eventId)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(
                    outboxEvent => outboxEvent.LeaseExpiresAt,
                    new DateTime(
                        2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

}

public sealed class OutboxIdentityTests
{
    [Theory]
    [InlineData(0, 1, 10, false)]
    [InlineData(3, 4, 80, false)]
    [InlineData(4, 5, 0, true)]
    public void DispatchFailure_SchedulesRetryOrDeadLettersAtFifthAttempt(
        int currentRetryCount,
        int expectedRetryCount,
        int expectedDelaySeconds,
        bool expectedDeadLetter)
    {
        var failedAt = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
        var outboxEvent = new OutboxEvent
        {
            RetryCount = currentRetryCount,
            ClaimedBy = "node-a",
            ClaimedAt = failedAt.AddSeconds(-1),
            LeaseExpiresAt = failedAt.AddMinutes(1)
        };

        OutboxProcessorBackgroundService.RecordDispatchFailure(
            outboxEvent,
            new InvalidOperationException("integration failure"),
            failedAt);

        Assert.Equal(expectedRetryCount, outboxEvent.RetryCount);
        Assert.Equal(expectedDeadLetter, outboxEvent.IsDeadLetter);
        Assert.Equal(
            expectedDeadLetter ? null : failedAt.AddSeconds(expectedDelaySeconds),
            outboxEvent.NextAttemptAt);
        Assert.Null(outboxEvent.ClaimedBy);
        Assert.Null(outboxEvent.ClaimedAt);
        Assert.Null(outboxEvent.LeaseExpiresAt);
    }

    [Fact]
    public void StaffEventRetry_ReusesDurableEventIdentity()
    {
        var eventId = Guid.NewGuid();
        var outboxEvent = new OutboxEvent
        {
            Id = eventId,
            Type = "StaffDataChanged",
            PayloadJson = """{"schemaVersion":"2","scopes":["hr"]}"""
        };

        var firstIdentity =
            OutboxProcessorBackgroundService.EnsureStableStaffEventId(outboxEvent);
        var firstPayload = outboxEvent.PayloadJson;
        var retryIdentity =
            OutboxProcessorBackgroundService.EnsureStableStaffEventId(outboxEvent);

        Assert.Equal(eventId, firstIdentity);
        Assert.Equal(firstIdentity, retryIdentity);
        Assert.Equal(firstPayload, outboxEvent.PayloadJson);
    }

    [Fact]
    public async Task QueueRetry_ReusesOutboxEventAsExternalJobIdentity()
    {
        var eventId = Guid.NewGuid();
        var outboxEvent = new OutboxEvent
        {
            Id = eventId,
            Type = "CodeActivated",
            TargetUserId = Guid.NewGuid().ToString(),
            PayloadJson = """{"source":"outbox-lease-test"}"""
        };
        var enqueuer = new CapturingJobEnqueuer();

        await ParentPurchaseOutboxDispatcher.DispatchAsync(outboxEvent, enqueuer);
        await ParentPurchaseOutboxDispatcher.DispatchAsync(outboxEvent, enqueuer);

        Assert.Equal(2, enqueuer.Payloads.Count);
        Assert.All(
            enqueuer.Payloads,
            payload => Assert.Equal(
                eventId.ToString(),
                RedisJobEnqueuer.ResolveStableJobId("notifications", payload)));
    }

    private sealed class CapturingJobEnqueuer : IJobEnqueuer
    {
        public List<string> Payloads { get; } = [];

        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            Payloads.Add(JsonSerializer.Serialize(data));
            return Task.CompletedTask;
        }
    }
}
