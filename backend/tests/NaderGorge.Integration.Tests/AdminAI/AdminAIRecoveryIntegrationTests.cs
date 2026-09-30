using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIRecoveryIntegrationTests
{
    [Fact]
    public async Task RestartSweep_RecoversLostQueueWorkerAndCallbackWithoutTouchingCompletedTurn()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seedDb = fixture.CreateDbContext();
        await seedDb.Database.MigrateAsync();
        var seed = Seed(seedDb);

        var cancelled = AddTurn(seedDb, seed, 1, AdminAITurnStatus.CancelRequested);
        cancelled.CancellationRequestedAt = DateTime.UtcNow.AddMinutes(-1);
        var cancelledStep = AddStep(seedDb, cancelled, AdminAITurnStepStatus.ProviderRunning, DateTime.UtcNow.AddMinutes(-3));
        cancelledStep.NextCallbackAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        var queued = AddTurn(seedDb, seed, 2, AdminAITurnStatus.Queued);
        queued.QueuedAt = DateTime.UtcNow.AddMinutes(-3);
        var claimed = AddTurn(seedDb, seed, 3, AdminAITurnStatus.Planning);
        var claimedStep = AddStep(seedDb, claimed, AdminAITurnStepStatus.Claimed, DateTime.UtcNow.AddMinutes(-3));
        var provider = AddTurn(seedDb, seed, 4, AdminAITurnStatus.Answering);
        var providerStep = AddStep(seedDb, provider, AdminAITurnStepStatus.ProviderRunning, DateTime.UtcNow.AddMinutes(-3));
        var reads = AddTurn(seedDb, seed, 5, AdminAITurnStatus.Retrieving);
        var readsStep = AddStep(seedDb, reads, AdminAITurnStepStatus.ReadsCompleted, DateTime.UtcNow.AddMinutes(-3));
        var active = AddTurn(seedDb, seed, 8, AdminAITurnStatus.Planning);
        var activeStep = AddStep(seedDb, active, AdminAITurnStepStatus.ProviderRunning, DateTime.UtcNow.AddMinutes(-3));
        activeStep.NextCallbackAttemptAt = DateTime.UtcNow.AddMinutes(1);
        var callback = AddTurn(seedDb, seed, 6, AdminAITurnStatus.Answering);
        var callbackStep = AddStep(seedDb, callback, AdminAITurnStepStatus.ProviderRunning, DateTime.UtcNow);
        callbackStep.CallbackStatus = "Pending";
        callbackStep.CallbackAttemptCount = 5;
        callbackStep.NextCallbackAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        var completed = AddTurn(seedDb, seed, 7, AdminAITurnStatus.Completed);
        completed.CompletedAt = DateTime.UtcNow.AddMinutes(-1);
        await seedDb.SaveChangesAsync();

        await using (var restartedDb = fixture.CreateDbContext())
            Assert.Equal(6, await new AdminAIRecoveryService(restartedDb).ReconcileAsync(20, default));

        await using var verifyDb = fixture.CreateDbContext();
        var turns = await verifyDb.AdminAITurns.AsNoTracking().ToDictionaryAsync(x => x.Id);
        var steps = await verifyDb.AdminAITurnSteps.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Equal(AdminAITurnStatus.Cancelled, turns[cancelled.Id].Status);
        Assert.Equal("CANCELLED", turns[cancelled.Id].FailureCode);
        Assert.Equal(AdminAITurnStepStatus.Cancelled, steps[cancelledStep.Id].Status);
        Assert.Equal("CANCELLED", steps[cancelledStep.Id].FailureCode);
        Assert.Equal("admin_ai_queue_stale", turns[queued.Id].FailureCode);
        foreach (var step in new[] { claimedStep, providerStep, readsStep })
        {
            Assert.Equal(AdminAITurnStepStatus.Failed, steps[step.Id].Status);
            Assert.Equal("admin_ai_worker_lease_expired", steps[step.Id].FailureCode);
            Assert.Equal("admin_ai_worker_lease_expired", turns[step.TurnId].FailureCode);
        }
        Assert.Equal("Failed", steps[callbackStep.Id].CallbackStatus);
        Assert.Equal("CALLBACK_UNAVAILABLE", turns[callback.Id].FailureCode);
        Assert.Equal(AdminAITurnStatus.Planning, turns[active.Id].Status);
        Assert.Equal(AdminAITurnStepStatus.ProviderRunning, steps[activeStep.Id].Status);
        Assert.Equal(AdminAITurnStatus.Completed, turns[completed.Id].Status);
        Assert.Null(turns[completed.Id].FailureCode);
        Assert.Equal(0, await new AdminAIRecoveryService(verifyDb).ReconcileAsync(20, default));
    }

    [Fact]
    public async Task RestartSweep_PreservesTerminalEffectsAndQuarantinesUncertainExecutions()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seedDb = fixture.CreateDbContext();
        await seedDb.Database.MigrateAsync();
        var seed = Seed(seedDb);
        var turn = AddTurn(seedDb, seed, 1, AdminAITurnStatus.Completed);
        turn.CompletedAt = DateTime.UtcNow.AddMinutes(-7);

        var claimed = AddProposal(seedDb, seed, turn, AdminAIProposalStatus.Executing);
        var claimedExecution = AddExecution(seedDb, claimed, AdminAIExecutionStatus.Claimed);
        var executing = AddProposal(seedDb, seed, turn, AdminAIProposalStatus.Executing);
        var executingExecution = AddExecution(seedDb, executing, AdminAIExecutionStatus.Executing);
        var finished = AddProposal(seedDb, seed, turn, AdminAIProposalStatus.Succeeded);
        var finishedExecution = AddExecution(seedDb, finished, AdminAIExecutionStatus.Succeeded);
        finishedExecution.CompletedAt = DateTime.UtcNow.AddMinutes(-7);
        var expiring = AddProposal(seedDb, seed, turn, AdminAIProposalStatus.PendingConfirmation);
        expiring.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        var challenge = new AdminAIConfirmationChallenge
        {
            ProposalId = expiring.Id, PhraseDigest = new string('e', 64),
            ExpiresAt = expiring.ExpiresAt, Status = AdminAIChallengeStatus.Pending
        };
        var secure = AddProposal(seedDb, seed, turn, AdminAIProposalStatus.PendingSecureInput);
        secure.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        var grant = new AdminAISecureInputGrant
        {
            ProposalId = secure.Id, ActorAdminUserId = seed.Actor.Id,
            InputKind = "test", TokenDigest = new string('f', 64),
            ProtectedPayload = [1, 2, 3], PayloadHash = new string('9', 64),
            ExpiresAt = secure.ExpiresAt, Status = AdminAISecureInputGrantStatus.Submitted
        };
        seedDb.AddRange(challenge, grant);
        await seedDb.SaveChangesAsync();

        await using (var restartedDb = fixture.CreateDbContext())
            Assert.Equal(5, await new AdminAIRecoveryService(restartedDb).ReconcileAsync(20, default));

        await using var verifyDb = fixture.CreateDbContext();
        var proposals = await verifyDb.AdminAIActionProposals.AsNoTracking().ToDictionaryAsync(x => x.Id);
        var executions = await verifyDb.AdminAIActionExecutions.AsNoTracking().ToDictionaryAsync(x => x.Id);
        foreach (var item in new[] { (claimed, claimedExecution), (executing, executingExecution) })
        {
            Assert.Equal(AdminAIProposalStatus.RecoveryRequired, proposals[item.Item1.Id].Status);
            Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, executions[item.Item2.Id].Status);
            Assert.Equal("authoritative_outcome_unknown_after_restart", executions[item.Item2.Id].FailureCode);
        }
        Assert.Equal(AdminAIProposalStatus.Succeeded, proposals[finished.Id].Status);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, executions[finishedExecution.Id].Status);
        Assert.Equal(AdminAIProposalStatus.Expired, proposals[expiring.Id].Status);
        Assert.Equal(AdminAIProposalStatus.Expired, proposals[secure.Id].Status);
        Assert.Equal(AdminAIChallengeStatus.Expired,
            (await verifyDb.AdminAIConfirmationChallenges.SingleAsync(x => x.Id == challenge.Id)).Status);
        var purged = await verifyDb.AdminAISecureInputGrants.SingleAsync(x => x.Id == grant.Id);
        Assert.Null(purged.ProtectedPayload);
        Assert.Null(purged.PayloadHash);
        Assert.Equal(AdminAISecureInputGrantStatus.Expired, purged.Status);
        Assert.Equal(0, await new AdminAIRecoveryService(verifyDb).ReconcileAsync(20, default));
    }

    private static AdminAIActionProposal AddProposal(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        (User Actor, AdminAICapabilityBaseline Baseline, AdminAISensitiveDataPolicyVersion Policy, AdminAIConversation Conversation) seed,
        AdminAITurn turn, AdminAIProposalStatus status)
    {
        var proposal = new AdminAIActionProposal
        {
            ConversationId = seed.Conversation.Id, TurnId = turn.Id,
            ActorAdminUserId = seed.Actor.Id, CapabilityBaselineId = seed.Baseline.Id,
            SensitiveDataPolicyVersionId = seed.Policy.Id, CapabilityKey = "test.recovery",
            CapabilityVersion = "1", Status = status,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            PayloadHash = new string('a', 64), StateFingerprint = "state-v1"
        };
        db.Add(proposal);
        return proposal;
    }

    private static AdminAIActionExecution AddExecution(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        AdminAIActionProposal proposal, AdminAIExecutionStatus status)
    {
        var execution = new AdminAIActionExecution
        {
            ProposalId = proposal.Id, ActorAdminUserId = proposal.ActorAdminUserId,
            CapabilityKey = proposal.CapabilityKey, CapabilityVersion = proposal.CapabilityVersion,
            IdempotencyDigest = Guid.NewGuid().ToString("N").PadRight(64, 'a'),
            PayloadHash = proposal.PayloadHash, AuthoritativeOperation = "test.recovery",
            Status = status, TraceId = "test", ExternalOperationId = Guid.NewGuid().ToString("N"),
            ClaimedAt = DateTime.UtcNow.AddMinutes(-7)
        };
        db.Add(execution);
        return execution;
    }

    private static (User Actor, AdminAICapabilityBaseline Baseline, AdminAISensitiveDataPolicyVersion Policy, AdminAIConversation Conversation) Seed(
        NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var role = new Role { Name = "Admin AI recovery test", Type = RoleType.Admin };
        var actor = new User
        {
            FullName = "Admin AI recovery test", PhoneNumber = "01000000170", PasswordHash = "test",
            IsActive = true, UserRoles = [new UserRole { Role = role }]
        };
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "recovery-test", ManifestHash = new string('a', 64), SourceRevision = "test",
            RuntimeInventoryHash = new string('b', 64), FrontendInventoryHash = new string('c', 64),
            Status = AdminAICapabilityBaselineStatus.Active
        };
        var policy = new AdminAISensitiveDataPolicyVersion
        {
            Version = "recovery-test", PolicyHash = new string('d', 64),
            Status = AdminAISensitiveDataPolicyStatus.Active
        };
        var conversation = new AdminAIConversation { OwnerAdminUserId = actor.Id, Title = "Recovery test" };
        db.AddRange(actor, baseline, policy, conversation);
        return (actor, baseline, policy, conversation);
    }

    private static AdminAITurn AddTurn(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        (User Actor, AdminAICapabilityBaseline Baseline, AdminAISensitiveDataPolicyVersion Policy, AdminAIConversation Conversation) seed,
        int sequence,
        AdminAITurnStatus status)
    {
        var source = new AdminAIMessage
        {
            ConversationId = seed.Conversation.Id, Sequence = sequence,
            Role = AdminAIMessageRole.Admin, Content = $"Recovery test {sequence}"
        };
        var turn = new AdminAITurn
        {
            ConversationId = seed.Conversation.Id, SourceMessageId = source.Id,
            ActorAdminUserId = seed.Actor.Id, CapabilityBaselineId = seed.Baseline.Id,
            SensitiveDataPolicyVersionId = seed.Policy.Id, CallbackIdempotencyDigest = sequence.ToString("x64"),
            Status = status
        };
        db.AddRange(source, turn);
        return turn;
    }

    private static AdminAITurnStep AddStep(
        NaderGorge.Infrastructure.Data.AppDbContext db, AdminAITurn turn,
        AdminAITurnStepStatus status, DateTime startedAt)
    {
        var step = new AdminAITurnStep
        {
            TurnId = turn.Id, StepNumber = 1, Status = status,
            StartedAt = startedAt, CallbackStatus = "Claimed"
        };
        db.Add(step);
        return step;
    }
}
