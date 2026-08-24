using NaderGorge.Infrastructure.Background;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIOutboxRecoveryTests
{
    [Fact]
    public void AdminAITurnQueue_HasClosedMappingAndStableJobIdentityAcrossReplay()
    {
        var turnId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { turnId, conversationId = Guid.NewGuid() });

        Assert.Equal("admin ai turn", RedisJobEnqueuer.ResolveJobType("ai-admin-agent-turns", "respond"));
        Assert.Equal($"admin-ai-turn-{turnId:D}", RedisJobEnqueuer.ResolveStableJobId("ai-admin-agent-turns", payload));
        Assert.Equal(
            RedisJobEnqueuer.ResolveStableJobId("ai-admin-agent-turns", payload),
            RedisJobEnqueuer.ResolveStableJobId("ai-admin-agent-turns", payload));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("complete")]
    public void AdminAITurnQueue_RejectsUnknownJobs(string jobName) =>
        Assert.Throws<InvalidOperationException>(() => RedisJobEnqueuer.ResolveJobType("ai-admin-agent-turns", jobName));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"turnId\":\"not-a-guid\"}")]
    public void AdminAITurnQueue_RejectsMissingOrInvalidTurnIdentity(string payload) =>
        Assert.Throws<InvalidOperationException>(() => RedisJobEnqueuer.ResolveStableJobId("ai-admin-agent-turns", payload));

    [Fact]
    public async Task Recovery_ExpiresPendingProposalAndStaleLeaseInOneBoundedBatch()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-recovery-{Guid.NewGuid()}").Options);
        db.AdminAIActionProposals.Add(new AdminAIActionProposal
        {
            Status = AdminAIProposalStatus.PendingConfirmation, ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CapabilityKey = "blocked", CapabilityVersion = "1", PayloadHash = new string('a', 64), StateFingerprint = new string('b', 64)
        });
        db.AdminAITurnSteps.Add(new AdminAITurnStep
        {
            Status = AdminAITurnStepStatus.Claimed, StartedAt = DateTime.UtcNow.AddMinutes(-3), StepNumber = 1
        });
        await db.SaveChangesAsync();

        var changed = await new AdminAIRecoveryService(db).ReconcileAsync(2, default);

        Assert.Equal(2, changed);
        Assert.Equal(AdminAIProposalStatus.Expired, db.AdminAIActionProposals.Single().Status);
        Assert.Equal(AdminAITurnStepStatus.Failed, db.AdminAITurnSteps.Single().Status);
        Assert.Equal("admin_ai_worker_lease_expired", db.AdminAITurnSteps.Single().FailureCode);
        Assert.Equal(0, await new AdminAIRecoveryService(db).ReconcileAsync(2, default));
    }

    [Fact]
    public async Task Recovery_RejectsUnboundedBatch()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-recovery-{Guid.NewGuid()}").Options);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new AdminAIRecoveryService(db).ReconcileAsync(501, default));
    }

    [Fact]
    public async Task Recovery_RevokesActiveTurnWhenAdminAuthorityDisappears()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-revoked-{Guid.NewGuid()}").Options);
        var turn = new AdminAITurn { ActorAdminUserId = Guid.NewGuid(), Status = AdminAITurnStatus.Planning, CallbackIdempotencyDigest = new string('a', 64) }; db.Add(turn); await db.SaveChangesAsync();
        Assert.Equal(1, await new AdminAIRecoveryService(db).ReconcileAsync(1, default));
        Assert.Equal(AdminAITurnStatus.AccessRevoked, turn.Status); Assert.Equal("admin_ai_access_revoked", turn.FailureCode);
    }

    [Fact]
    public async Task Recovery_CancellationOutranksLeaseFailureAndIsReplaySafe()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-cancel-{Guid.NewGuid()}").Options);
        var turn = new AdminAITurn
        {
            Status = AdminAITurnStatus.CancelRequested,
            CancellationRequestedAt = DateTime.UtcNow.AddSeconds(-5),
            CallbackIdempotencyDigest = new string('c', 64)
        };
        db.Add(turn);
        await db.SaveChangesAsync();

        Assert.Equal(1, await new AdminAIRecoveryService(db).ReconcileAsync(1, default));
        Assert.Equal(AdminAITurnStatus.Cancelled, turn.Status);
        Assert.Equal("CANCELLED", turn.FailureCode);
        Assert.Equal(0, await new AdminAIRecoveryService(db).ReconcileAsync(1, default));
    }

    [Fact]
    public async Task Recovery_FailsQueuedTurnWhoseDeliveryWasLost()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-stale-queue-{Guid.NewGuid()}").Options);
        var actorId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = actorId,
            UserRoles = [new UserRole { Role = new Role { Type = RoleType.Admin } }]
        });
        var turn = new AdminAITurn
        {
            Status = AdminAITurnStatus.Queued,
            QueuedAt = DateTime.UtcNow.AddMinutes(-3),
            ActorAdminUserId = actorId,
            CallbackIdempotencyDigest = new string('e', 64)
        };
        db.Add(turn);
        await db.SaveChangesAsync();

        Assert.Equal(1, await new AdminAIRecoveryService(db).ReconcileAsync(10, default));
        Assert.Equal(AdminAITurnStatus.Failed, turn.Status);
        Assert.Equal("admin_ai_queue_stale", turn.FailureCode);
        Assert.NotNull(turn.CompletedAt);
        Assert.Equal(0, await new AdminAIRecoveryService(db).ReconcileAsync(10, default));
    }

    [Fact]
    public async Task Recovery_FailsExhaustedCallbackWithoutReplayingBusinessWork()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-callback-{Guid.NewGuid()}").Options);
        var turn = new AdminAITurn { Status = AdminAITurnStatus.Answering, CallbackIdempotencyDigest = new string('d', 64) };
        var step = new AdminAITurnStep
        {
            TurnId = turn.Id,
            Turn = turn,
            StepNumber = 1,
            Status = AdminAITurnStepStatus.ProviderRunning,
            CallbackStatus = "Pending",
            CallbackAttemptCount = 5,
            NextCallbackAttemptAt = DateTime.UtcNow.AddSeconds(-1)
        };
        db.AddRange(turn, step);
        await db.SaveChangesAsync();

        Assert.Equal(1, await new AdminAIRecoveryService(db).ReconcileAsync(10, default));
        Assert.Equal("Failed", step.CallbackStatus);
        Assert.Equal(AdminAITurnStatus.Failed, turn.Status);
        Assert.Equal("CALLBACK_UNAVAILABLE", turn.FailureCode);
    }

    [Fact]
    public async Task Recovery_ProductionRegression_August20_ReadCompletedTurnDoesNotBlockFutureChats()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-read-stall-{Guid.NewGuid()}").Options);
        var actorId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = actorId,
            UserRoles = [new UserRole { Role = new Role { Type = RoleType.Admin } }]
        });
        var turn = new AdminAITurn
        {
            ActorAdminUserId = actorId,
            Status = AdminAITurnStatus.Retrieving,
            CallbackIdempotencyDigest = new string('f', 64)
        };
        var step = new AdminAITurnStep
        {
            TurnId = turn.Id,
            Turn = turn,
            StepNumber = 1,
            Status = AdminAITurnStepStatus.ReadsCompleted,
            CallbackStatus = "Claimed",
            StartedAt = DateTime.UtcNow.AddMinutes(-3),
            NextCallbackAttemptAt = DateTime.UtcNow.AddMinutes(-2)
        };
        db.AddRange(turn, step);
        await db.SaveChangesAsync();

        Assert.Equal(1, await new AdminAIRecoveryService(db).ReconcileAsync(10, default));
        Assert.Equal(AdminAITurnStepStatus.Failed, step.Status);
        Assert.Equal(AdminAITurnStatus.Failed, turn.Status);
        Assert.Equal("admin_ai_worker_lease_expired", turn.FailureCode);
    }
}
