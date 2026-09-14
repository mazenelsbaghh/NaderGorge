using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIExternalActionTests
{
    [Theory]
    [InlineData("provider")]
    [InlineData("job")]
    [InlineData("file")]
    public async Task Timeout_PersistsOneDeterministicExternalIdentity_ForAuthoritativeRecovery(string externalKind)
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var protector = new TestProtector();
        var protectedPayload = protector.Protect("proposal-payload", Encoding.UTF8.GetBytes($"{{\"kind\":\"{externalKind}\"}}"));
        var proposal = Proposal(actorId, $"external.{externalKind}", protectedPayload);
        db.AdminAIActionProposals.Add(proposal);
        await db.SaveChangesAsync();
        var adapter = new TimeoutExternalAction(proposal.CapabilityKey);
        var audit = new CapturingAudit();
        var executor = new AdminAIActionExecutor(db, new AllowAdmin(actorId), protector, new NoSecureInput(), [adapter], audit);

        var first = await executor.ExecuteAsync(actorId, proposal.Id, $"intent-{externalKind}", default);
        var replay = await executor.ExecuteAsync(actorId, proposal.Id, $"intent-{externalKind}", default);

        var execution = Assert.Single(db.AdminAIActionExecutions);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, execution.Status);
        Assert.Equal(execution.Id.ToString("N"), execution.ExternalOperationId);
        Assert.Equal(execution.ExternalOperationId, adapter.SeenOperationId);
        Assert.Equal(1, adapter.ExecuteCount);
        Assert.Equal(["ExecutionStarted", "ExecutionRecoveryRequired"], audit.EventTypes);
    }

    [Fact]
    public async Task Reconciliation_UsesOnlyPersistedProviderIdentity_AndNeverReissuesEffect()
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var protector = new TestProtector();
        var protectedPayload = protector.Protect("proposal-payload", "{}"u8);
        var proposal = Proposal(actorId, "external.provider", protectedPayload);
        db.AdminAIActionProposals.Add(proposal);
        await db.SaveChangesAsync();
        var adapter = new TimeoutExternalAction(proposal.CapabilityKey);
        var executor = new AdminAIActionExecutor(db, new AllowAdmin(actorId), protector, new NoSecureInput(), [adapter]);
        await executor.ExecuteAsync(actorId, proposal.Id, "intent-provider", default);
        var execution = Assert.Single(db.AdminAIActionExecutions);
        var resolver = new AuthoritativeResolver(proposal.CapabilityKey);
        var audit = new CapturingAudit();

        Assert.Equal(1, await new AdminAIExternalOperationReconciler(db, [resolver], audit).ReconcileAsync(10, default));

        Assert.Equal(execution.ExternalOperationId, resolver.SeenExternalOperationId);
        Assert.Equal(execution.Id.ToString("N"), resolver.SeenExecutionId);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(AdminAIProposalStatus.Succeeded, proposal.Status);
        Assert.Equal(1, adapter.ExecuteCount);
        Assert.Equal(["ExecutionSucceeded"], audit.EventTypes);
    }

    [Fact]
    public async Task UnknownOrTimedOutAuthoritativeResult_RemainsRecoveryRequired()
    {
        await using var db = CreateDb();
        var proposal = Proposal(Guid.NewGuid(), "external.file", new TestProtector().Protect("proposal-payload", "{}"u8));
        var execution = RecoveryExecution(proposal, "file-object-42");
        db.AddRange(proposal, execution);
        await db.SaveChangesAsync();

        Assert.Equal(0, await new AdminAIExternalOperationReconciler(db, [new UnknownResolver(proposal.CapabilityKey)]).ReconcileAsync(10, default));
        Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, execution.Status);
        Assert.Null(execution.CompletedAt);

        Assert.Equal(0, await new AdminAIExternalOperationReconciler(db, [new TimeoutResolver(proposal.CapabilityKey)]).ReconcileAsync(10, default));
        Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, execution.Status);
        Assert.Null(execution.CompletedAt);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-external-{Guid.NewGuid():N}").Options);

    private static AdminAIActionProposal Proposal(Guid actorId, string capabilityKey, AdminAIProtectedValue payload) => new()
    {
        ActorAdminUserId = actorId,
        CapabilityKey = capabilityKey,
        CapabilityVersion = "1",
        Status = AdminAIProposalStatus.Confirming,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        ProtectedNormalizedPayload = payload.Ciphertext,
        PayloadHash = payload.Digest,
        StateFingerprint = "state-v1"
    };

    private static AdminAIActionExecution RecoveryExecution(AdminAIActionProposal proposal, string externalId) => new()
    {
        ProposalId = proposal.Id,
        ActorAdminUserId = proposal.ActorAdminUserId,
        CapabilityKey = proposal.CapabilityKey,
        CapabilityVersion = "1",
        IdempotencyDigest = new string('a', 64),
        PayloadHash = proposal.PayloadHash,
        AuthoritativeOperation = "fake-provider",
        Status = AdminAIExecutionStatus.RecoveryRequired,
        ExternalOperationId = externalId,
        TraceId = "trace",
        ClaimedAt = DateTime.UtcNow
    };

    private sealed class TimeoutExternalAction(string key) : IAdminAIActionCapability
    {
        public string Key => key;
        public int ExecuteCount { get; private set; }
        public string? SeenOperationId { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) =>
            Task.FromResult(new AdminAIActionPreview("external", "external:safe", new { }, new { }, new { }, new { valid = true }, "state-v1"));
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
        {
            ExecuteCount++;
            SeenOperationId = operationId;
            throw new TimeoutException("ambiguous provider timeout");
        }
    }

    private sealed class AuthoritativeResolver(string key) : IAdminAIExternalResultResolver
    {
        public string CapabilityKey => key;
        public string? SeenExternalOperationId { get; private set; }
        public string? SeenExecutionId { get; private set; }
        public Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId, CancellationToken ct)
        {
            SeenExternalOperationId = externalOperationId;
            SeenExecutionId = executionId;
            return Task.FromResult<AdminAIActionOutcome?>(new(AdminAIExecutionStatus.Succeeded, new { reconciled = true }, 1, ["external"]));
        }
    }

    private sealed class UnknownResolver(string key) : IAdminAIExternalResultResolver
    {
        public string CapabilityKey => key;
        public Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId, CancellationToken ct) => Task.FromResult<AdminAIActionOutcome?>(null);
    }

    private sealed class TimeoutResolver(string key) : IAdminAIExternalResultResolver
    {
        public string CapabilityKey => key;
        public Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId, CancellationToken ct) => throw new TimeoutException();
    }

    private sealed class AllowAdmin(Guid actorId) : IAdminAIAccessGate
    {
        public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion, CancellationToken ct) =>
            userId == actorId
                ? Task.FromResult(new AdminAIAccessSnapshot(userId, expectedSecurityVersion ?? 1, DateTime.UtcNow))
                : throw new UnauthorizedAccessException();
    }

    private sealed class TestProtector : IAdminAIDataProtector
    {
        public AdminAIProtectedValue Protect(string purpose, ReadOnlySpan<byte> plaintext) => new(plaintext.ToArray(), Digest(purpose, plaintext));
        public byte[] Unprotect(string purpose, AdminAIProtectedValue value) => value.Ciphertext;
        public string Digest(string purpose, ReadOnlySpan<byte> value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{Convert.ToHexString(value)}"))).ToLowerInvariant();
        public string NormalizeConfirmationPhrase(string value) => value.Normalize().Trim();
    }

    private sealed class NoSecureInput : IAdminAISecureInputService
    {
        public Task<AdminAISecureGrantResult> IssueAsync(Guid actorId, Guid proposalId, string inputKind, long expectedProposalVersion, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAISecureGrantResult> SubmitAsync(Guid actorId, Guid grantId, string token, string inputKind, ReadOnlyMemory<byte> payload, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAIProtectedValue> ConsumeAsync(Guid actorId, Guid proposalId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CapturingAudit : IAdminAIAuditWriter
    {
        public List<string> EventTypes { get; } = [];
        public Task WriteAsync(string eventType, Guid? actorId, Guid? conversationId, Guid? turnId, Guid? proposalId, object safeEvidence, CancellationToken ct)
        {
            EventTypes.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
