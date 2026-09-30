using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIActionConcurrencyTests
{
    [Fact]
    public async Task AmbiguousAuthoritativeFailure_PreservesClaimAndNeverReissuesEffect()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seedDb = fixture.CreateDbContext();
        await seedDb.Database.MigrateAsync();
        var actor = new User
        {
            Id = Guid.NewGuid(), FullName = "مدير اختبار", PhoneNumber = "01000000169",
            PasswordHash = "test", IsActive = true
        };
        var baseline = new AdminAICapabilityBaseline
        {
            Id = Guid.NewGuid(), Version = "test-1", ManifestHash = new string('a', 64),
            SourceRevision = "test", RuntimeInventoryHash = new string('b', 64),
            FrontendInventoryHash = new string('c', 64), Status = AdminAICapabilityBaselineStatus.Active
        };
        var policy = new AdminAISensitiveDataPolicyVersion
        {
            Id = Guid.NewGuid(), Version = "test-1", PolicyHash = new string('d', 64),
            Status = AdminAISensitiveDataPolicyStatus.Active
        };
        var conversation = new AdminAIConversation
        {
            Id = Guid.NewGuid(), OwnerAdminUserId = actor.Id, Title = "تأكيد اختبار"
        };
        var sourceMessage = new AdminAIMessage
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id, Sequence = 1,
            Role = AdminAIMessageRole.Admin, Content = "اختبار فقط"
        };
        var turn = new AdminAITurn
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id, SourceMessageId = sourceMessage.Id,
            ActorAdminUserId = actor.Id, CapabilityBaselineId = baseline.Id,
            SensitiveDataPolicyVersionId = policy.Id, CallbackIdempotencyDigest = new string('e', 64)
        };
        var protector = new TestProtector();
        var payload = protector.Protect("proposal-payload", "{}"u8);
        var proposal = new AdminAIActionProposal
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id, TurnId = turn.Id,
            ActorAdminUserId = actor.Id, CapabilityBaselineId = baseline.Id,
            SensitiveDataPolicyVersionId = policy.Id, CapabilityKey = "test.ambiguous",
            CapabilityVersion = "1", Status = AdminAIProposalStatus.Confirming,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ProtectedNormalizedPayload = payload.Ciphertext, PayloadHash = payload.Digest,
            StateFingerprint = "state-v1"
        };
        seedDb.AddRange(actor, baseline, policy, conversation, sourceMessage, turn, proposal);
        await seedDb.SaveChangesAsync();

        var adapter = new AmbiguousAction();
        await using (var db = fixture.CreateDbContext())
        {
            var executor = new AdminAIActionExecutor(db, new AllowAdmin(actor.Id), protector, new NoSecureInput(), [adapter]);
            var first = await executor.ExecuteAsync(actor.Id, proposal.Id, "same-intent", default);
            Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, first.Status);
        }
        await using (var db = fixture.CreateDbContext())
        {
            var executor = new AdminAIActionExecutor(db, new AllowAdmin(actor.Id), protector, new NoSecureInput(), [adapter]);
            var replay = await executor.ExecuteAsync(actor.Id, proposal.Id, "same-intent", default);
            Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, replay.Status);
        }

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(1, adapter.ExecuteCount);
        Assert.Single(await verifyDb.AdminAIActionExecutions.ToListAsync());

        var strandedProposal = new AdminAIActionProposal
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id, TurnId = turn.Id,
            ActorAdminUserId = actor.Id, CapabilityBaselineId = baseline.Id,
            SensitiveDataPolicyVersionId = policy.Id, CapabilityKey = "test.stranded",
            CapabilityVersion = "1", Status = AdminAIProposalStatus.Executing,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ProtectedNormalizedPayload = payload.Ciphertext, PayloadHash = payload.Digest,
            StateFingerprint = "state-v1"
        };
        var strandedExecution = new AdminAIActionExecution
        {
            Id = Guid.NewGuid(), ProposalId = strandedProposal.Id,
            ActorAdminUserId = actor.Id, CapabilityKey = strandedProposal.CapabilityKey,
            CapabilityVersion = "1", IdempotencyDigest = new string('9', 64),
            PayloadHash = payload.Digest, AuthoritativeOperation = "test",
            Status = AdminAIExecutionStatus.Claimed,
            ExternalOperationId = Guid.NewGuid().ToString("N"), TraceId = "test",
            ClaimedAt = DateTime.UtcNow.AddMinutes(-6)
        };
        verifyDb.AddRange(strandedProposal, strandedExecution);
        await verifyDb.SaveChangesAsync();

        Assert.True(await new AdminAIRecoveryService(verifyDb).ReconcileAsync(10, default) >= 1);
        Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, strandedExecution.Status);
        Assert.Equal(AdminAIProposalStatus.RecoveryRequired, strandedProposal.Status);
        Assert.Equal(0, await new AdminAIRecoveryService(verifyDb).ReconcileAsync(10, default));
    }

    private sealed class AmbiguousAction : IAdminAIActionCapability
    {
        public string Key => "test.ambiguous";
        public int ExecuteCount { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) =>
            Task.FromResult(new AdminAIActionPreview("test", "safe", new { }, new { }, new { }, new { valid = true }, "state-v1"));
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
        {
            ExecuteCount++;
            throw new IOException("The effect may already have happened.");
        }
    }

    private sealed class AllowAdmin(Guid actorId) : IAdminAIAccessGate
    {
        public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion, CancellationToken ct) =>
            userId == actorId ? Task.FromResult(new AdminAIAccessSnapshot(userId, 1, DateTime.UtcNow)) : throw new UnauthorizedAccessException();
    }

    private sealed class TestProtector : IAdminAIDataProtector
    {
        public AdminAIProtectedValue Protect(string purpose, ReadOnlySpan<byte> plaintext) => new(plaintext.ToArray(), Digest(purpose, plaintext));
        public byte[] Unprotect(string purpose, AdminAIProtectedValue value) => value.Ciphertext;
        public string Digest(string purpose, ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{Convert.ToHexString(value)}")));
        public string NormalizeConfirmationPhrase(string value) => value.Normalize().Trim();
    }

    private sealed class NoSecureInput : IAdminAISecureInputService
    {
        public Task<AdminAISecureGrantResult> IssueAsync(Guid actorId, Guid proposalId, string inputKind, long expectedProposalVersion, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAISecureGrantResult> SubmitAsync(Guid actorId, Guid grantId, string token, string inputKind, ReadOnlyMemory<byte> payload, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAIProtectedValue> ConsumeAsync(Guid actorId, Guid proposalId, CancellationToken ct) => throw new NotSupportedException();
    }
}
