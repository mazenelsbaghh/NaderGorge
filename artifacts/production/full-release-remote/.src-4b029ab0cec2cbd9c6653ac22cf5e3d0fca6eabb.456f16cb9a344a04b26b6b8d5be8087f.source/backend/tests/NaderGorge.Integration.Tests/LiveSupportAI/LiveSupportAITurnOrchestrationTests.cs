using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Integration.Tests.LiveSupport;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Integration.Tests.LiveSupportAI;

public sealed class LiveSupportAITurnOrchestrationTests
{
    [Fact]
    public async Task Message_turn_and_outbox_commit_or_rollback_together_without_redis()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var user = await CreateUserAsync(fixture.Db);
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 14601,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "test",
            CreatedByUserId = user.Id,
            Version = 1
        };
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Student,
            StudentUserId = user.Id,
            LinkedStudentUserId = user.Id,
            Status = LiveSupportConversationStatus.Waiting,
            Version = 1
        };
        fixture.Db.AddRange(policy, conversation);
        fixture.Db.LiveSupportAIConversationStates.Add(new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = policy.Id,
            Mode = LiveSupportAIMode.AiActive,
            LastParticipantActivityAt = DateTime.UtcNow,
            Version = 1
        });
        await fixture.Db.SaveChangesAsync();
        var orchestrator = new LiveSupportAITurnOrchestrator(fixture.Db, new UnusedContextBuilder());

        var committedMessage = NewMessage(conversation.Id, user.Id, "commit-message");
        fixture.Db.LiveSupportMessages.Add(committedMessage);
        await orchestrator.QueueForParticipantMessageAsync(conversation.Id, committedMessage.Id, CancellationToken.None);
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(1, await fixture.Db.LiveSupportAITurns.CountAsync(turn => turn.SourceMessageId == committedMessage.Id));
        Assert.Equal(1, await fixture.Db.OutboxEvents.CountAsync(value => value.Type == "LiveSupportAITurnQueued"));

        await using (var transaction = await fixture.Db.Database.BeginTransactionAsync())
        {
            var rolledBackMessage = NewMessage(conversation.Id, user.Id, "rollback-message");
            fixture.Db.LiveSupportMessages.Add(rolledBackMessage);
            await orchestrator.QueueForParticipantMessageAsync(conversation.Id, rolledBackMessage.Id, CancellationToken.None);
            await fixture.Db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(1, await fixture.Db.LiveSupportMessages.CountAsync(message => message.ConversationId == conversation.Id));
        Assert.Equal(1, await fixture.Db.LiveSupportAITurns.CountAsync(turn => turn.ConversationId == conversation.Id));
        Assert.Equal(1, await fixture.Db.OutboxEvents.CountAsync(value => value.Type == "LiveSupportAITurnQueued"));
    }

    [Fact]
    public async Task Concurrent_claims_transition_queued_turn_once_and_are_idempotent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var turnId = await SeedTurnAsync(fixture, LiveSupportAITurnStatus.Queued, 1, null);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var claimTasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            await using var db = NewDbContext(fixture.ConnectionString);
            return await new LiveSupportAITurnOrchestrator(db, new FixedContextBuilder()).ClaimAsync(turnId, CancellationToken.None);
        })).ToArray();
        gate.SetResult();
        var claims = await Task.WhenAll(claimTasks);

        Assert.Single(claims, claim => claim is not null);
        Assert.Single(claims, claim => claim is null);
        fixture.Db.ChangeTracker.Clear();
        var turn = await fixture.Db.LiveSupportAITurns.SingleAsync(item => item.Id == turnId);
        Assert.Equal(LiveSupportAITurnStatus.Processing, turn.Status);
        Assert.Equal(2, turn.Version);
        Assert.NotNull(turn.StartedAt);
    }

    [Fact]
    public async Task Concurrent_claims_of_processing_turn_do_not_change_version()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var startedAt = DateTime.UtcNow.AddMinutes(-3);
        var turnId = await SeedTurnAsync(fixture, LiveSupportAITurnStatus.Processing, 7, startedAt);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var claimTasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            await using var db = NewDbContext(fixture.ConnectionString);
            return await new LiveSupportAITurnOrchestrator(db, new FixedContextBuilder()).ClaimAsync(turnId, CancellationToken.None);
        })).ToArray();
        gate.SetResult();
        var claims = await Task.WhenAll(claimTasks);

        Assert.Single(claims, claim => claim is not null);
        Assert.Single(claims, claim => claim is null);
        fixture.Db.ChangeTracker.Clear();
        var turn = await fixture.Db.LiveSupportAITurns.SingleAsync(item => item.Id == turnId);
        Assert.Equal(LiveSupportAITurnStatus.Processing, turn.Status);
        Assert.Equal(8, turn.Version);
        Assert.NotEqual(startedAt, turn.StartedAt);
    }

    private static async Task<Guid> SeedTurnAsync(
        PostgresLiveSupportFixture fixture,
        LiveSupportAITurnStatus status,
        long version,
        DateTime? startedAt)
    {
        var user = await CreateUserAsync(fixture.Db);
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = Random.Shared.Next(20_000, 30_000),
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "test",
            CreatedByUserId = user.Id,
            Version = 1
        };
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Student,
            StudentUserId = user.Id,
            LinkedStudentUserId = user.Id,
            Status = LiveSupportConversationStatus.Waiting,
            Version = 1
        };
        fixture.Db.AddRange(policy, conversation);
        await fixture.Db.SaveChangesAsync();

        var message = NewMessage(conversation.Id, user.Id, $"claim-{Guid.NewGuid():N}");
        fixture.Db.LiveSupportMessages.Add(message);
        await fixture.Db.SaveChangesAsync();

        var turn = new LiveSupportAITurn
        {
            ConversationId = conversation.Id,
            SourceMessageId = message.Id,
            PolicyVersionId = policy.Id,
            Status = status,
            CallbackStatus = LiveSupportAICallbackStatus.NotReady,
            QueuedAt = DateTime.UtcNow,
            StartedAt = startedAt,
            Version = version
        };
        fixture.Db.LiveSupportAITurns.Add(turn);
        await fixture.Db.SaveChangesAsync();
        return turn.Id;
    }

    private static AppDbContext NewDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static async Task<User> CreateUserAsync(AppDbContext database)
    {
        var user = new User
        {
            FullName = "Live Support Integration User",
            PhoneNumber = $"019{Random.Shared.NextInt64(10_000_000, 99_999_999)}",
            PasswordHash = "integration"
        };
        database.Users.Add(user);
        await database.SaveChangesAsync();
        return user;
    }

    private static LiveSupportMessage NewMessage(Guid conversationId, Guid userId, string clientMessageId) => new()
    {
        ConversationId = conversationId,
        SenderType = LiveSupportSenderType.Student,
        SenderUserId = userId,
        ClientMessageId = clientMessageId,
        Type = LiveSupportMessageType.Text,
        Content = "test",
        SentAt = DateTime.UtcNow
    };

    private sealed class UnusedContextBuilder : ILiveSupportAIContextBuilder
    {
        public Task<LiveSupportAIWorkerClaimDto> BuildAsync(Guid turnId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Context building is not part of queue atomicity.");
    }

    private sealed class FixedContextBuilder : ILiveSupportAIContextBuilder
    {
        public Task<LiveSupportAIWorkerClaimDto> BuildAsync(Guid turnId, CancellationToken cancellationToken) =>
            Task.FromResult(new LiveSupportAIWorkerClaimDto(
                "1", turnId, Guid.Empty, Guid.Empty, 1, turnId.ToString("N"), DateTime.UtcNow,
                "test", [], new Dictionary<string, object?>(), [], [], []));
    }
}
