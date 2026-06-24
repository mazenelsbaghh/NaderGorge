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

    private sealed class FakeAssignments : ILiveSupportAssignmentCoordinator
    {
        public int Calls { get; private set; }
        public Task AssignWaitingAsync(CancellationToken ct) { Calls++; return Task.CompletedTask; }
        public Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct) => Task.CompletedTask;
        public Task<LiveSupportConversationDto> TransferAsync(Guid actorUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct) => throw new NotSupportedException();
    }
}
