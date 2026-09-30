using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed partial class AdminAIActionConcurrencyTests
{
    [Fact]
    public async Task SuccessfulExecution_ReplaysSameIntentAndRejectsConflictingKey()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var seed = await SeedProposalAsync(fixture, "test.once");
        var adapter = new OnceAction();

        await using (var db = fixture.CreateDbContext())
        {
            var executor = NewExecutor(db, seed, adapter);
            var first = await executor.ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default);
            var replay = await executor.ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default);
            Assert.Equal(AdminAIExecutionStatus.Succeeded, first.Status);
            Assert.Equal(first.Id, replay.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync(seed.ActorId, seed.ProposalId, "different-intent", default));
        }

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(1, adapter.ExecuteCount);
        Assert.Single(await verifyDb.AdminAIActionExecutions.ToListAsync());
    }

    [Fact]
    public async Task TwoTabsConfirmingSameIntent_CommitOneClaimAndExecuteOnce()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var seed = await SeedProposalAsync(fixture, "test.once");
        var adapter = new OnceAction(delayMilliseconds: 250);
        await using var firstDb = fixture.CreateDbContext();
        await using var secondDb = fixture.CreateDbContext();
        var first = NewExecutor(firstDb, seed, adapter).ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default);
        var second = NewExecutor(secondDb, seed, adapter).ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default);

        var results = await Task.WhenAll(first, second);

        Assert.Contains(results, result => result.Status == AdminAIExecutionStatus.Succeeded);
        Assert.All(results, result => Assert.True(result.Status is
            AdminAIExecutionStatus.Claimed or AdminAIExecutionStatus.Succeeded));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(1, adapter.ExecuteCount);
        await using var verifyDb = fixture.CreateDbContext();
        Assert.Single(await verifyDb.AdminAIActionExecutions.ToListAsync());
        var terminalReplay = await NewExecutor(verifyDb, seed, adapter)
            .ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, terminalReplay.Status);
        Assert.Equal(results[0].Id, terminalReplay.Id);
    }

    [Fact]
    public async Task StaleState_InvalidatesProposalWithoutExecutingBusinessEffect()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var seed = await SeedProposalAsync(fixture, "test.once");
        var adapter = new OnceAction(stateFingerprint: "changed-state");
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewExecutor(db, seed, adapter).ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default));

        Assert.Equal(0, adapter.ExecuteCount);
        Assert.Empty(await db.AdminAIActionExecutions.ToListAsync());
        Assert.Equal(AdminAIProposalStatus.Invalidated,
            (await db.AdminAIActionProposals.SingleAsync(item => item.Id == seed.ProposalId)).Status);
    }

    [Fact]
    public async Task RemovedTarget_PersistsInvalidationWithoutClaimOrBusinessEffect()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var seed = await SeedProposalAsync(fixture, "test.once");
        var adapter = new RemovedTargetAction();
        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NewExecutor(db, seed, adapter).ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default));

        await using var verifyDb = fixture.CreateDbContext();
        var proposal = await verifyDb.AdminAIActionProposals.AsNoTracking()
            .SingleAsync(item => item.Id == seed.ProposalId);
        Assert.Equal(AdminAIProposalStatus.Invalidated, proposal.Status);
        Assert.Equal("stale_state", proposal.InvalidatedReasonCode);
        Assert.Empty(await verifyDb.AdminAIActionExecutions.ToListAsync());
        Assert.Equal(0, adapter.ExecuteCount);
    }

    [Fact]
    public async Task ReusedIntentOnDifferentProposal_RejectsPayloadConflict()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var seed = await SeedProposalAsync(fixture, "test.once");
        var adapter = new OnceAction();
        Guid secondProposalId;
        await using (var db = fixture.CreateDbContext())
        {
            var original = await db.AdminAIActionProposals.AsNoTracking().SingleAsync(item => item.Id == seed.ProposalId);
            var different = seed.Protector.Protect("proposal-payload", "{\"different\":true}"u8);
            var second = new AdminAIActionProposal
            {
                Id = Guid.NewGuid(), ConversationId = original.ConversationId, TurnId = original.TurnId,
                ActorAdminUserId = seed.ActorId, CapabilityBaselineId = original.CapabilityBaselineId,
                SensitiveDataPolicyVersionId = original.SensitiveDataPolicyVersionId,
                CapabilityKey = "test.once", CapabilityVersion = "1", Status = AdminAIProposalStatus.Confirming,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5), ProtectedNormalizedPayload = different.Ciphertext,
                PayloadHash = different.Digest, StateFingerprint = "state-v1"
            };
            secondProposalId = second.Id;
            db.AdminAIActionProposals.Add(second);
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.CreateDbContext())
            Assert.Equal(AdminAIExecutionStatus.Succeeded,
                (await NewExecutor(db, seed, adapter).ExecuteAsync(seed.ActorId, seed.ProposalId, "same-intent", default)).Status);
        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NewExecutor(db, seed, adapter).ExecuteAsync(seed.ActorId, secondProposalId, "same-intent", default));

        Assert.Equal(1, adapter.ExecuteCount);
    }

    [Fact]
    public async Task AnotherAdmin_CannotExecuteOwnersProposal()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var seed = await SeedProposalAsync(fixture, "test.once");
        var otherAdmin = Guid.NewGuid();
        var adapter = new OnceAction();
        await using var db = fixture.CreateDbContext();
        var executor = new AdminAIActionExecutor(db, new AllowTwoAdmins(seed.ActorId, otherAdmin),
            seed.Protector, new NoSecureInput(), [adapter]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            executor.ExecuteAsync(otherAdmin, seed.ProposalId, "other-intent", default));

        Assert.Equal(0, adapter.ExecuteCount);
        Assert.Empty(await db.AdminAIActionExecutions.ToListAsync());
    }

    [Fact]
    public async Task CommittedClaim_ReleasesTransactionBeforeDatabaseBackedAdapterRuns()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var seed = await SeedProposalAsync(fixture, "test.db-write");
        await using (var db = fixture.CreateDbContext())
        {
            var adapter = new DatabaseWriteAction(db);
            var result = await NewExecutor(db, seed, adapter)
                .ExecuteAsync(seed.ActorId, seed.ProposalId, "write-once", default);
            Assert.Equal(AdminAIExecutionStatus.Succeeded, result.Status);
        }

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Single(await verifyDb.StudentNotes.Where(note => note.Content == "admin-ai-concurrency-test").ToListAsync());
    }

    private static AdminAIActionExecutor NewExecutor(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        ProposalSeed seed,
        IAdminAIActionCapability adapter) =>
        new(db, new AllowAdmin(seed.ActorId), seed.Protector, new NoSecureInput(), [adapter]);

    private static async Task<ProposalSeed> SeedProposalAsync(PostgresAdminAIFixture fixture, string capabilityKey)
    {
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var actor = new User
        {
            Id = Guid.NewGuid(), FullName = "مدير اختبار", PhoneNumber = "01000000170",
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
            SensitiveDataPolicyVersionId = policy.Id, CapabilityKey = capabilityKey,
            CapabilityVersion = "1", Status = AdminAIProposalStatus.Confirming,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ProtectedNormalizedPayload = payload.Ciphertext, PayloadHash = payload.Digest,
            StateFingerprint = "state-v1"
        };
        db.AddRange(actor, baseline, policy, conversation, sourceMessage, turn, proposal);
        await db.SaveChangesAsync();
        return new ProposalSeed(actor.Id, proposal.Id, protector);
    }

    private sealed record ProposalSeed(Guid ActorId, Guid ProposalId, TestProtector Protector);

    private sealed class OnceAction(int delayMilliseconds = 0, string stateFingerprint = "state-v1") : IAdminAIActionCapability
    {
        private int _executeCount;
        public string Key => "test.once";
        public int ExecuteCount => Volatile.Read(ref _executeCount);
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) =>
            Task.FromResult(new AdminAIActionPreview("test", "safe", new { }, new { }, new { }, new { valid = true }, stateFingerprint));
        public async Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
        {
            Interlocked.Increment(ref _executeCount);
            if (delayMilliseconds > 0) await Task.Delay(delayMilliseconds, ct);
            return AdminAIActionOutcomeFactory.Success(new { done = true }, 1, ["test"]);
        }
    }

    private sealed class RemovedTargetAction : IAdminAIActionCapability
    {
        public string Key => "test.once";
        public int ExecuteCount { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) =>
            throw new AdminAIActionPreviewUnavailableException("Target was removed after proposal preview.");
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
        {
            ExecuteCount++;
            throw new InvalidOperationException("A removed target must not be changed.");
        }
    }

    private sealed class AllowTwoAdmins(Guid owner, Guid other) : IAdminAIAccessGate
    {
        public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion, CancellationToken ct) =>
            userId == owner || userId == other
                ? Task.FromResult(new AdminAIAccessSnapshot(userId, 1, DateTime.UtcNow))
                : throw new UnauthorizedAccessException();
    }

    private sealed class DatabaseWriteAction(NaderGorge.Infrastructure.Data.AppDbContext db) : IAdminAIActionCapability
    {
        public string Key => "test.db-write";
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) =>
            Task.FromResult(new AdminAIActionPreview("test", "safe", new { }, new { }, new { }, new { valid = true }, "state-v1"));
        public async Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
        {
            db.StudentNotes.Add(new StudentNote
            {
                StudentId = actorId, AdminId = actorId, Content = "admin-ai-concurrency-test"
            });
            await db.SaveChangesAsync(ct);
            return AdminAIActionOutcomeFactory.Success(new { done = true }, 1, ["student-notes"]);
        }
    }
}
