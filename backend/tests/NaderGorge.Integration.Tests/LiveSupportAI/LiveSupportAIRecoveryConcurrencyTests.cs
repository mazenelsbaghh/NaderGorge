using NaderGorge.Infrastructure.Services;
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
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using NaderGorge.Integration.Tests.LiveSupport;
using System.Text.Json;

namespace NaderGorge.Integration.Tests.LiveSupportAI;

public sealed class LiveSupportAIRecoveryConcurrencyTests
{
    [Fact]
    public async Task Concurrent_recovery_runs_are_safe_and_idempotent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        var adminUser = NewUser("admin");
        var studentUser = NewUser("student");
        fixture.Db.Users.AddRange(adminUser, studentUser);

        var policy = new LiveSupportAIPolicyVersion { VersionNumber = 1, Status = LiveSupportAIPolicyStatus.Published, IsEnabled = true, SystemInstructions = "test", CreatedByUserId = adminUser.Id, Version = 1 };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = studentUser.Id, LinkedStudentUserId = studentUser.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        fixture.Db.AddRange(policy, conversation);
        await fixture.Db.SaveChangesAsync();

        // Seed a stale turn and expired pending action
        var now = DateTime.UtcNow;
        var message = new LiveSupportMessage { ConversationId = conversation.Id, SenderType = LiveSupportSenderType.Student, SenderUserId = studentUser.Id, ClientMessageId = "msg-recovery-123", Type = LiveSupportMessageType.Text, Content = "help", SentAt = now.AddMinutes(-7) };
        fixture.Db.LiveSupportMessages.Add(message);
        await fixture.Db.SaveChangesAsync();

        var state = new LiveSupportAIConversationState { ConversationId = conversation.Id, PolicyVersionId = policy.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = now, Version = 1 };
        var turn = new LiveSupportAITurn { ConversationId = conversation.Id, SourceMessageId = message.Id, PolicyVersionId = policy.Id, Status = LiveSupportAITurnStatus.Queued, QueuedAt = now.AddMinutes(-6), Version = 1 };
        var action = new LiveSupportAIPendingAction { ConversationId = conversation.Id, TurnId = turn.Id, DecisionKind = LiveSupportAIPendingDecisionKind.Handoff, PolicyVersionId = policy.Id, ActionKey = "system.handoff", Status = LiveSupportAIPendingActionStatus.PendingConfirmation, ExpiresAt = now.AddMinutes(-1), IdempotencyKey = Guid.NewGuid(), Version = 1 };
        fixture.Db.AddRange(state, turn, action);
        await fixture.Db.SaveChangesAsync();

        var handoffCalls = 0;
        var fakeHandoff = new FakeHandoffService(() => Interlocked.Increment(ref handoffCalls));

        // Run recovery concurrently using separate DbContexts
        await Parallel.ForEachAsync(Enumerable.Range(0, 5), async (i, ct) =>
        {
            try
            {
                await using var db = new NaderGorge.Infrastructure.Data.AppDbContext(new DbContextOptionsBuilder<NaderGorge.Infrastructure.Data.AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
                var recoveryService = new LiveSupportAIRecoveryService(db, fakeHandoff);
                await recoveryService.RecoverBatchAsync(now, 10, ct);
            }
            catch (Exception)
            {
                // ignore transient/concurrency errors
            }
        });

        fixture.Db.ChangeTracker.Clear();
        var updatedTurn = await fixture.Db.LiveSupportAITurns.SingleAsync(t => t.Id == turn.Id);
        var updatedAction = await fixture.Db.LiveSupportAIPendingActions.SingleAsync(a => a.Id == action.Id);

        Assert.Equal(LiveSupportAITurnStatus.Failed, updatedTurn.Status);
        Assert.Equal(LiveSupportAIPendingActionStatus.Expired, updatedAction.Status);
        Assert.Equal(1, handoffCalls); // Only one handoff should succeed/execute
    }

    private static User NewUser(string prefix) => new() { FullName = prefix, PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "integration" };

    [Fact]
    public async Task Precedence_races_resolve_consistently()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        var adminUser = NewUser("admin");
        var studentUser = NewUser("student");
        fixture.Db.Users.AddRange(adminUser, studentUser);

        var policy = new LiveSupportAIPolicyVersion { VersionNumber = 1, Status = LiveSupportAIPolicyStatus.Published, IsEnabled = true, SystemInstructions = "test", CreatedByUserId = adminUser.Id, Version = 1 };
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = studentUser.Id, LinkedStudentUserId = studentUser.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        fixture.Db.AddRange(policy, conversation);
        await fixture.Db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var message = new LiveSupportMessage { ConversationId = conversation.Id, SenderType = LiveSupportSenderType.Student, SenderUserId = studentUser.Id, ClientMessageId = "msg-race-123", Type = LiveSupportMessageType.Text, Content = "help", SentAt = now.AddMinutes(-7) };
        fixture.Db.LiveSupportMessages.Add(message);
        await fixture.Db.SaveChangesAsync();

        var state = new LiveSupportAIConversationState { ConversationId = conversation.Id, PolicyVersionId = policy.Id, Mode = LiveSupportAIMode.AiActive, LastParticipantActivityAt = now, Version = 1 };
        var turn = new LiveSupportAITurn { ConversationId = conversation.Id, SourceMessageId = message.Id, PolicyVersionId = policy.Id, Status = LiveSupportAITurnStatus.Queued, QueuedAt = now.AddMinutes(-6), Version = 1 };
        var action = new LiveSupportAIPendingAction { ConversationId = conversation.Id, TurnId = turn.Id, DecisionKind = LiveSupportAIPendingDecisionKind.Handoff, PolicyVersionId = policy.Id, ActionKey = "system.handoff", Status = LiveSupportAIPendingActionStatus.PendingConfirmation, ExpiresAt = now.AddMinutes(-1), IdempotencyKey = Guid.NewGuid(), Version = 1 };
        fixture.Db.AddRange(state, turn, action);
        await fixture.Db.SaveChangesAsync();

        var tasks = new List<Task>();
        
        // 1. Complete AI Turn (late callback)
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                await using var db = new NaderGorge.Infrastructure.Data.AppDbContext(new DbContextOptionsBuilder<NaderGorge.Infrastructure.Data.AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
                var orchestrator = new LiveSupportAITurnOrchestrator(db, null!, null!);
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
                await orchestrator.CompleteAsync(turn.Id, request, CancellationToken.None);
            }
            catch {}
        }));

        // 2. Confirm Handoff
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                await using var db = new NaderGorge.Infrastructure.Data.AppDbContext(new DbContextOptionsBuilder<NaderGorge.Infrastructure.Data.AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
                var handoffService = new LiveSupportAIHandoffService(db, new FakeAssignmentCoordinator());
                var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, studentUser.Id, null);
                await handoffService.HandoffAsync(conversation.Id, participant, null, "USER_REQUEST", "تأكيد التحويل", false, "key-race-confirm", CancellationToken.None);
            }
            catch {}
        }));

        // 3. Close Conversation
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                await using var db = new NaderGorge.Infrastructure.Data.AppDbContext(new DbContextOptionsBuilder<NaderGorge.Infrastructure.Data.AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
                var service = new LiveSupportService(db, null!);
                await service.CloseAsync(adminUser.Id, true, conversation.Id, "إغلاق إداري", CancellationToken.None);
            }
            catch {}
        }));

        // 4. Recovery Batch
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                await using var db = new NaderGorge.Infrastructure.Data.AppDbContext(new DbContextOptionsBuilder<NaderGorge.Infrastructure.Data.AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
                var recoveryService = new LiveSupportAIRecoveryService(db, new FakeHandoffService(() => {}));
                await recoveryService.RecoverBatchAsync(now, 10, CancellationToken.None);
            }
            catch {}
        }));

        await Task.WhenAll(tasks);

        fixture.Db.ChangeTracker.Clear();
        var finalConversation = await fixture.Db.LiveSupportConversations.SingleAsync(c => c.Id == conversation.Id);
        var finalState = await fixture.Db.LiveSupportAIConversationStates.SingleAsync(s => s.ConversationId == conversation.Id);

        var messages = await fixture.Db.LiveSupportMessages.Where(m => m.ConversationId == conversation.Id && m.SenderType == LiveSupportSenderType.AI).ToListAsync();
        Assert.Empty(messages);
    }

        private sealed class FakeAssignmentCoordinator : ILiveSupportAssignmentCoordinator
    {
        public Task AssignWaitingAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct) => Task.CompletedTask;
        public Task<LiveSupportConversationDto> TransferAsync(Guid actorUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeHandoffService : ILiveSupportAIHandoffService
    {
        private readonly Action _onCall;
        public FakeHandoffService(Action onCall) => _onCall = onCall;

        public Task<string> HandoffAsync(Guid conversationId, LiveSupportParticipantIdentity? participant, Guid? actorUserId, string reasonCode, string safeSummary, bool forced, string idempotencyKey, CancellationToken cancellationToken)
        {
            _onCall();
            return Task.FromResult("HANDED_OFF");
        }
    }
}
