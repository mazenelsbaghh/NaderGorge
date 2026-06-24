using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIRecoveryTests
{
    [Fact]
    public async Task Recovery_bounds_stale_turn_expired_decision_and_verification_then_requests_one_handoff()
    {
        await using var db = TestAppDbContextFactory.Create();
        var now = DateTime.UtcNow;
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        db.AddRange(conversation,
            new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = now, Version = 1 },
            new LiveSupportAITurn { ConversationId = conversation.Id, SourceMessageId = Guid.NewGuid(), PolicyVersionId = Guid.NewGuid(), Status = LiveSupportAITurnStatus.Processing, StartedAt = now.AddMinutes(-20), QueuedAt = now.AddMinutes(-21), Version = 1 },
            new LiveSupportAIPendingAction { ConversationId = conversation.Id, TurnId = Guid.NewGuid(), DecisionKind = LiveSupportAIPendingDecisionKind.Handoff, PolicyVersionId = Guid.NewGuid(), ActionKey = "system.handoff", Status = LiveSupportAIPendingActionStatus.PendingConfirmation, ExpiresAt = now.AddMinutes(-1), IdempotencyKey = Guid.NewGuid(), Version = 1 },
            new LiveSupportAIVerificationSession { ConversationId = conversation.Id, PolicyVersionId = Guid.NewGuid(), LookupKey = "pending", LookupValueHash = new string('0', 64), RequiredCorrect = 1, MaxAttempts = 3, Status = LiveSupportAIVerificationStatus.AwaitingLookup, ExpiresAt = now.AddMinutes(-1), Version = 1 });
        await db.SaveChangesAsync();
        var handoff = new FakeHandoff();

        var result = await new LiveSupportAIRecoveryService(db, handoff).RecoverBatchAsync(now, 10, default);

        Assert.Equal(1, result.StaleTurns);
        Assert.Equal(1, result.ExpiredDecisions);
        Assert.Equal(1, result.ExpiredVerifications);
        Assert.Equal(1, handoff.Calls);
        Assert.Equal(LiveSupportAITurnStatus.Failed, (await db.LiveSupportAITurns.SingleAsync()).Status);
    }


    [Fact]
    public async Task Recovery_handles_stale_queued_turn()
    {
        await using var db = TestAppDbContextFactory.Create();
        var now = DateTime.UtcNow;
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        var turn = new LiveSupportAITurn { ConversationId = conversation.Id, SourceMessageId = Guid.NewGuid(), PolicyVersionId = Guid.NewGuid(), Status = LiveSupportAITurnStatus.Queued, QueuedAt = now.AddMinutes(-6), Version = 1 };
        db.AddRange(conversation,
            new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = now, Version = 1 },
            turn);
        await db.SaveChangesAsync();
        var handoff = new FakeHandoff();

        var result = await new LiveSupportAIRecoveryService(db, handoff).RecoverBatchAsync(now, 10, default);

        Assert.Equal(1, result.StaleTurns);
        Assert.Equal(LiveSupportAITurnStatus.Failed, (await db.LiveSupportAITurns.SingleAsync()).Status);
    }

    [Fact]
    public async Task Recovery_handles_inactivity_warning()
    {
        await using var db = TestAppDbContextFactory.Create();
        var now = DateTime.UtcNow;
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        var state = new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = now.AddMinutes(-31), Version = 1 };
        db.AddRange(conversation, state);
        await db.SaveChangesAsync();
        var handoff = new FakeHandoff();

        var result = await new LiveSupportAIRecoveryService(db, handoff).RecoverBatchAsync(now, 10, default);

        Assert.Equal(1, result.InactivityWarnings);
        var updatedState = await db.LiveSupportAIConversationStates.SingleAsync();
        Assert.NotNull(updatedState.InactivityWarningSentAt);
        Assert.NotNull(updatedState.AutoCloseAt);
    }

    [Fact]
    public async Task Recovery_reconciles_disable_requested()
    {
        await using var db = TestAppDbContextFactory.Create();
        var now = DateTime.UtcNow;
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        var state = new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, DisableRequestedAt = now.AddMinutes(-1), LastParticipantActivityAt = now, Version = 1 };
        db.AddRange(conversation, state);
        await db.SaveChangesAsync();
        var handoff = new FakeHandoff();

        var result = await new LiveSupportAIRecoveryService(db, handoff).RecoverBatchAsync(now, 10, default);

        Assert.Equal(1, result.ReconciledConversations);
        Assert.Equal(1, handoff.Calls);
    }

    [Fact]
    public async Task Recovery_handles_stale_provider_completed_turn()
    {
        await using var db = TestAppDbContextFactory.Create();
        var now = DateTime.UtcNow;
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        var turn = new LiveSupportAITurn { ConversationId = conversation.Id, SourceMessageId = Guid.NewGuid(), PolicyVersionId = Guid.NewGuid(), Status = LiveSupportAITurnStatus.ProviderCompleted, StartedAt = now.AddMinutes(-11), QueuedAt = now.AddMinutes(-21), Version = 1 };
        db.AddRange(conversation,
            new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = now, Version = 1 },
            turn);
        await db.SaveChangesAsync();
        var handoff = new FakeHandoff();

        var result = await new LiveSupportAIRecoveryService(db, handoff).RecoverBatchAsync(now, 10, default);

        Assert.Equal(1, result.StaleTurns);
        Assert.Equal(LiveSupportAITurnStatus.Failed, (await db.LiveSupportAITurns.SingleAsync()).Status);
    }

    private sealed class FakeHandoff : ILiveSupportAIHandoffService
    {
        public int Calls { get; private set; }
        public Task<string> HandoffAsync(Guid conversationId, LiveSupportParticipantIdentity? participant, Guid? actorUserId, string reasonCode, string safeSummary, bool forced, string idempotencyKey, CancellationToken cancellationToken) { Calls++; return Task.FromResult("HANDED_OFF"); }
    }
}
