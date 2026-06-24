using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
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
        var user = await fixture.Db.Users.FirstAsync();
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
}
