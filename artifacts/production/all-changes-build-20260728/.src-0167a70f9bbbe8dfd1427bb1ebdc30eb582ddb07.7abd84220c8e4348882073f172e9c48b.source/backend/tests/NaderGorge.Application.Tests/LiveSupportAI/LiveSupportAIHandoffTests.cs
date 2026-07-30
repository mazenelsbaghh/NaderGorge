using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Common;
using NaderGorge.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIHandoffTests
{
    [Fact]
    public async Task Confirmed_handoff_is_idempotent_invalidates_AI_work_and_queues_once()
    {
        await using var db = TestAppDbContextFactory.Create();
        var guest = new LiveSupportGuestSession { DisplayName = "زائر", PhoneNumber = "01000000146", SecurityStampHash = "stamp", CreatedIpHash = "ip", LastSeenAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, GuestSessionId = guest.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        db.AddRange(guest, conversation,
            new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = DateTime.UtcNow, Version = 1 },
            new LiveSupportAIPendingAction { ConversationId = conversation.Id, TurnId = Guid.NewGuid(), DecisionKind = LiveSupportAIPendingDecisionKind.Handoff, PolicyVersionId = Guid.NewGuid(), ActionKey = "system.handoff", Status = LiveSupportAIPendingActionStatus.PendingConfirmation, ExpiresAt = DateTime.UtcNow.AddMinutes(5), IdempotencyKey = Guid.NewGuid(), Version = 1 });
        await db.SaveChangesAsync();
        var assignments = new FakeAssignments();
        var service = new LiveSupportAIHandoffService(db, assignments);
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guest.Id);

        Assert.Equal("HANDED_OFF", await service.HandoffAsync(conversation.Id, participant, null, "USER_REQUEST", "طلب موظف", false, "same", default));
        Assert.Equal("REPLAYED", await service.HandoffAsync(conversation.Id, participant, null, "USER_REQUEST", "طلب موظف", false, "same", default));

        Assert.Equal(1, await db.LiveSupportQueueEntries.CountAsync(item => item.ConversationId == conversation.Id && item.DequeuedAt == null));
        Assert.Equal(LiveSupportAIMode.HumanQueued, (await db.LiveSupportAIConversationStates.SingleAsync()).Mode);
        Assert.Equal(1, assignments.Calls);
    }


    [Fact]
    public async Task Forced_handoff_does_not_require_pending_action_and_succeeds()
    {
        await using var db = TestAppDbContextFactory.Create();
        var guest = new LiveSupportGuestSession { DisplayName = "زائر", PhoneNumber = "01000000146", SecurityStampHash = "stamp", CreatedIpHash = "ip", LastSeenAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, GuestSessionId = guest.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        db.AddRange(guest, conversation,
            new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = DateTime.UtcNow, Version = 1 });
        await db.SaveChangesAsync();
        var assignments = new FakeAssignments();
        var service = new LiveSupportAIHandoffService(db, assignments);
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guest.Id);

        var result = await service.HandoffAsync(conversation.Id, participant, null, "CRITICAL_FAILURE", "فشل المساعد", true, "key-forced", default);
        Assert.Equal("HANDED_OFF", result);
        Assert.Equal(1, await db.LiveSupportQueueEntries.CountAsync(item => item.ConversationId == conversation.Id && item.DequeuedAt == null));
        Assert.Equal(LiveSupportAIMode.HumanQueued, (await db.LiveSupportAIConversationStates.SingleAsync()).Mode);
    }

    [Fact]
    public async Task Handoff_throws_if_not_forced_and_no_pending_action()
    {
        await using var db = TestAppDbContextFactory.Create();
        var guest = new LiveSupportGuestSession { DisplayName = "زائر", PhoneNumber = "01000000146", SecurityStampHash = "stamp", CreatedIpHash = "ip", LastSeenAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, GuestSessionId = guest.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        db.AddRange(guest, conversation,
            new LiveSupportAIConversationState { ConversationId = conversation.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = DateTime.UtcNow, Version = 1 });
        await db.SaveChangesAsync();
        var assignments = new FakeAssignments();
        var service = new LiveSupportAIHandoffService(db, assignments);
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guest.Id);

        await Assert.ThrowsAsync<NaderGorge.Application.Features.LiveSupport.Interfaces.LiveSupportException>(() =>
            service.HandoffAsync(conversation.Id, participant, null, "USER_REQUEST", "طلب موظف", false, "key-not-forced", default));
    }


    [Fact]
    public async Task CancelHandoffAsync_sets_status_to_cancelled_and_queues_new_ai_turn()
    {
        await using var db = TestAppDbContextFactory.Create();
        var guest = new LiveSupportGuestSession { DisplayName = "زائر", PhoneNumber = "01000000146", SecurityStampHash = "stamp", CreatedIpHash = "ip", LastSeenAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Guest, GuestSessionId = guest.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        var policy = new LiveSupportAIPolicyVersion { VersionNumber = 1, Status = LiveSupportAIPolicyStatus.Published, IsEnabled = true, SystemInstructions = "test", CreatedByUserId = Guid.NewGuid(), Version = 1 };
        
        var pendingAction = new LiveSupportAIPendingAction 
        { 
            ConversationId = conversation.Id, 
            TurnId = Guid.NewGuid(), 
            DecisionKind = LiveSupportAIPendingDecisionKind.Handoff, 
            PolicyVersionId = policy.Id, 
            ActionKey = "system.handoff", 
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation, 
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), 
            IdempotencyKey = Guid.NewGuid(), 
            Version = 1 
        };

        db.AddRange(guest, conversation, policy,
            new LiveSupportAIConversationState { ConversationId = conversation.Id, PolicyVersionId = policy.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = DateTime.UtcNow, Version = 1 },
            pendingAction);
        await db.SaveChangesAsync();

        var service = new LiveSupportService(db, new EnabledSettingsReader(), jobEnqueuer: new FakeJobEnqueuer(), aiTurnOrchestrator: new NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAITurnOrchestrator(db, null!, null!));
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guest.Id);

        await service.CancelHandoffAsync(participant, conversation.Id, default);

        var updatedAction = await db.LiveSupportAIPendingActions.FindAsync(pendingAction.Id);
        Assert.Equal(LiveSupportAIPendingActionStatus.Cancelled, updatedAction!.Status);

        var newTurn = await db.LiveSupportAITurns.FirstOrDefaultAsync(t => t.ConversationId == conversation.Id);
        Assert.NotNull(newTurn);
        Assert.Equal(LiveSupportAITurnStatus.Queued, newTurn.Status);
    }

    private sealed class FakeAssignments : ILiveSupportAssignmentCoordinator
    {
        public int Calls { get; private set; }
        public Task AssignWaitingAsync(CancellationToken ct) { Calls++; return Task.CompletedTask; }
        public Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct) => Task.CompletedTask;
        public Task<LiveSupportConversationDto> TransferAsync(Guid actorUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class EnabledSettingsReader : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
        public void Invalidate() { }
    }

    private sealed class FakeJobEnqueuer : IJobEnqueuer
    {
        public List<(string queueName, string jobName, object data)> EnqueuedJobs { get; } = new();
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            EnqueuedJobs.Add((queueName, jobName, data!));
            return Task.CompletedTask;
        }
    }

}
