using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIExternalRecoveryTests
{
    [Fact]
    public async Task RecoveryRequired_ChangesOnlyFromAuthoritativeExternalIdentity()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var proposal = AdminAIStrongConfirmationTests.Proposal(Guid.NewGuid()); db.Add(proposal);
        var execution = new AdminAIActionExecution { ProposalId = proposal.Id, ActorAdminUserId = proposal.ActorAdminUserId, CapabilityKey = "external.test", CapabilityVersion = "1", Status = AdminAIExecutionStatus.RecoveryRequired, ExternalOperationId = "provider-job-1", IdempotencyDigest = new string('a', 64), PayloadHash = proposal.PayloadHash, AuthoritativeOperation = "Fake", TraceId = "trace", ClaimedAt = DateTime.UtcNow }; db.Add(execution); await db.SaveChangesAsync();
        Assert.Equal(0, await new AdminAIExternalOperationReconciler(db, []).ReconcileAsync(10, default)); Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, execution.Status);
        var resolver = new Resolver(); Assert.Equal(1, await new AdminAIExternalOperationReconciler(db, [resolver]).ReconcileAsync(10, default));
        Assert.Equal("provider-job-1", resolver.SeenExternalId); Assert.Equal(execution.Id.ToString("N"), resolver.SeenExecutionId); Assert.Equal(AdminAIExecutionStatus.Succeeded, execution.Status); Assert.Equal(AdminAIProposalStatus.Succeeded, proposal.Status);
    }

    [Fact]
    public async Task ResolverTimeout_LeavesOutcomeAmbiguousAndRetryable()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var proposal = AdminAIStrongConfirmationTests.Proposal(Guid.NewGuid()); db.Add(proposal);
        var execution = new AdminAIActionExecution { ProposalId = proposal.Id, ActorAdminUserId = proposal.ActorAdminUserId, CapabilityKey = "external.timeout", CapabilityVersion = "1", Status = AdminAIExecutionStatus.RecoveryRequired, ExternalOperationId = "provider-job-2", IdempotencyDigest = new string('a', 64), PayloadHash = proposal.PayloadHash, AuthoritativeOperation = "Fake", TraceId = "trace", ClaimedAt = DateTime.UtcNow }; db.Add(execution); await db.SaveChangesAsync();

        Assert.Equal(0, await new AdminAIExternalOperationReconciler(db, [new TimeoutResolver()]).ReconcileAsync(10, default));
        Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, execution.Status);
        Assert.Equal(AdminAIProposalStatus.PendingConfirmation, proposal.Status);
    }

    [Fact]
    public void DuplicateResolvers_AreRejectedFailClosed()
    {
        using var db = AdminAIStrongConfirmationTests.CreateDb();
        Assert.Throws<InvalidOperationException>(() => new AdminAIExternalOperationReconciler(db, [new Resolver(), new Resolver()]));
    }

    private sealed class Resolver : IAdminAIExternalResultResolver
    {
        public string CapabilityKey => "external.test"; public string? SeenExternalId { get; private set; } public string? SeenExecutionId { get; private set; }
        public Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId, CancellationToken ct) { SeenExternalId = externalOperationId; SeenExecutionId = executionId; return Task.FromResult<AdminAIActionOutcome?>(new(AdminAIExecutionStatus.Succeeded, new { recovered = true }, 1, ["external"])); }
    }
    private sealed class TimeoutResolver : IAdminAIExternalResultResolver
    {
        public string CapabilityKey => "external.timeout";
        public Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId, CancellationToken ct) => throw new TimeoutException();
    }
}
