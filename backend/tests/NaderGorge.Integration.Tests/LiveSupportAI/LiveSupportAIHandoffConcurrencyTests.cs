using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using NaderGorge.Integration.Tests.LiveSupport;
using System.Text.Json;

namespace NaderGorge.Integration.Tests.LiveSupportAI;

public sealed class LiveSupportAIHandoffConcurrencyTests
{
    [Fact]
    public async Task Callback_after_handoff_is_discarded_and_creates_no_message()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        var studentUser = NewUser("student");
        var adminUser = NewUser("admin");
        fixture.Db.Users.AddRange(studentUser, adminUser);

        var policy = new LiveSupportAIPolicyVersion { VersionNumber = 1, Status = LiveSupportAIPolicyStatus.Published, IsEnabled = true, SystemInstructions = "test", CreatedByUserId = adminUser.Id, Version = 1 };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = studentUser.Id, LinkedStudentUserId = studentUser.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        fixture.Db.AddRange(policy, conversation);
        await fixture.Db.SaveChangesAsync();

        var state = new LiveSupportAIConversationState { ConversationId = conversation.Id, PolicyVersionId = policy.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = DateTime.UtcNow, Version = 1 };
        fixture.Db.LiveSupportAIConversationStates.Add(state);
        await fixture.Db.SaveChangesAsync();

        var message = new LiveSupportMessage { ConversationId = conversation.Id, SenderType = LiveSupportSenderType.Student, SenderUserId = studentUser.Id, ClientMessageId = "msg-123", Type = LiveSupportMessageType.Text, Content = "help", SentAt = DateTime.UtcNow };
        fixture.Db.LiveSupportMessages.Add(message);
        await fixture.Db.SaveChangesAsync();

        var orchestrator = new LiveSupportAITurnOrchestrator(fixture.Db, null!, null!);
        await orchestrator.QueueForParticipantMessageAsync(conversation.Id, message.Id, CancellationToken.None);
        await fixture.Db.SaveChangesAsync();

        var turn = await fixture.Db.LiveSupportAITurns.FirstAsync(t => t.ConversationId == conversation.Id);

        // Perform handoff
        var handoffService = new LiveSupportAIHandoffService(fixture.Db, new FakeAssignmentCoordinator());
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, studentUser.Id, null);
        
        var proposal = new LiveSupportAIPendingAction 
        { 
            ConversationId = conversation.Id, 
            TurnId = turn.Id, 
            DecisionKind = LiveSupportAIPendingDecisionKind.Handoff, 
            PolicyVersionId = policy.Id, 
            ActionKey = "system.handoff", 
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation, 
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), 
            IdempotencyKey = Guid.NewGuid(), 
            Version = 1 
        };
        fixture.Db.LiveSupportAIPendingActions.Add(proposal);
        await fixture.Db.SaveChangesAsync();

        var handoffResult = await handoffService.HandoffAsync(conversation.Id, participant, null, "USER_REQUEST", "تحويل يدوي", false, "key-handoff", CancellationToken.None);
        Assert.Equal("HANDED_OFF", handoffResult);

        // Try complete AI turn after handoff
        var request = new LiveSupportAIWorkerCompletionDto(
            SchemaVersion: "1",
            ExpectedConversationVersion: conversation.Version,
            ExpectedPolicyVersionId: policy.Id,
            DecisionHash: LiveSupportAITurnOrchestrator.ComputeDecisionHash(new LiveSupportAIWorkerDecisionDto("1", "reply", "مرحبا", null, null, null, null, null)),
            Decision: new LiveSupportAIWorkerDecisionDto("1", "reply", "مرحبا", null, null, null, null, null),
            Provider: "test",
            Model: "test",
            ProviderResponseId: "resp-123",
            InputTokenCount: 10,
            OutputTokenCount: 10,
            LatencyMs: 100,
            CallbackIdempotencyKey: turn.Id.ToString()
        );

        var completionResult = await orchestrator.CompleteAsync(turn.Id, request, CancellationToken.None);
        Assert.Equal("IDEMPOTENCY_CONFLICT", completionResult);

        // Assert no AI message was created
        var messagesCount = await fixture.Db.LiveSupportMessages.CountAsync(m => m.SenderType == LiveSupportSenderType.AI);
        Assert.Equal(0, messagesCount);
    }

    [Fact]
    public async Task Duplicate_queue_race_creates_only_one_queue_entry()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        var studentUser = NewUser("student");
        var adminUser = NewUser("admin");
        fixture.Db.Users.AddRange(studentUser, adminUser);

        var policy = new LiveSupportAIPolicyVersion { VersionNumber = 1, Status = LiveSupportAIPolicyStatus.Published, IsEnabled = true, SystemInstructions = "test", CreatedByUserId = adminUser.Id, Version = 1 };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = studentUser.Id, LinkedStudentUserId = studentUser.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        fixture.Db.AddRange(policy, conversation);
        await fixture.Db.SaveChangesAsync();

        var state = new LiveSupportAIConversationState { ConversationId = conversation.Id, PolicyVersionId = policy.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = DateTime.UtcNow, Version = 1 };
        fixture.Db.LiveSupportAIConversationStates.Add(state);
        await fixture.Db.SaveChangesAsync();

        var handoffService = new LiveSupportAIHandoffService(fixture.Db, new FakeAssignmentCoordinator());
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, studentUser.Id, null);

        var results = new List<string>();
        await Parallel.ForEachAsync(Enumerable.Range(0, 5), async (i, ct) =>
        {
            try
            {
                await using var db = new NaderGorge.Infrastructure.Data.AppDbContext(new DbContextOptionsBuilder<NaderGorge.Infrastructure.Data.AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
                var service = new LiveSupportAIHandoffService(db, new FakeAssignmentCoordinator());
                var res = await service.HandoffAsync(conversation.Id, participant, null, "USER_REQUEST", "تحويل متزامن", true, $"key-dup-{i}", ct);
                lock (results) { results.Add(res); }
            }
            catch (Exception)
            {
                // ignore serialization/concurrency exceptions
            }
        });

        fixture.Db.ChangeTracker.Clear();
        var queueEntries = await fixture.Db.LiveSupportQueueEntries.Where(q => q.ConversationId == conversation.Id && q.DequeuedAt == null).ToListAsync();
        Assert.Single(queueEntries);
    }

    private static User NewUser(string prefix) => new() { FullName = prefix, PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "integration" };

    private sealed class FakeAssignmentCoordinator : ILiveSupportAssignmentCoordinator
    {
        public Task AssignWaitingAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct) => Task.CompletedTask;
        public Task<LiveSupportConversationDto> TransferAsync(Guid actorUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct) => throw new NotImplementedException();
    }
}
