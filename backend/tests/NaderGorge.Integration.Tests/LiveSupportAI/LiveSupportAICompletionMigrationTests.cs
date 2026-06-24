using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Integration.Tests.LiveSupport;

namespace NaderGorge.Integration.Tests.LiveSupportAI;

public sealed class LiveSupportAICompletionMigrationTests
{
    private const string PreviousMigration = "20260624121729_AddAllowedDomainAndNavbarToRoles";

    [Fact]
    public async Task Upgrade_preserves_existing_support_identity_and_backfills_handoff_kind()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.Db.Database.EnsureDeletedAsync();
        var migrator = fixture.Db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var userId = await fixture.Db.Users.AsNoTracking().Select(x => x.Id).FirstAsync();
        var conversationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        const string messageContent = "legacy transcript remains unchanged";

        await fixture.Db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO live_support_conversations
                ("Id", "ParticipantType", "StudentUserId", "LinkedStudentUserId", "Status", "LastMessageAt", "Version", "CreatedAt")
            VALUES
                ({{conversationId}}, 0, {{userId}}, {{userId}}, 1, {{createdAt}}, 1, {{createdAt}});

            INSERT INTO live_support_ai_policy_versions
                ("Id", "VersionNumber", "Status", "IsEnabled", "SystemInstructions", "ReadableDataKeysJson", "ActionKeysJson", "LookupKeysJson", "VerificationQuestionKeysJson", "VerificationRequiredCorrect", "VerificationMaxAttempts", "PendingActionExpirySeconds", "InactivityMinutes", "InactivityWarningGraceSeconds", "CreatedByUserId", "Version", "CreatedAt")
            VALUES
                ({{policyId}}, 99146, 1, FALSE, 'legacy', '[]', '[]', '[]', '[]', 1, 3, 300, 30, 120, {{userId}}, 1, {{createdAt}});

            INSERT INTO live_support_messages
                ("Id", "ConversationId", "SenderType", "SenderUserId", "ClientMessageId", "Type", "Content", "SentAt", "CreatedAt")
            VALUES
                ({{messageId}}, {{conversationId}}, 0, {{userId}}, 'legacy-message-146', 0, {{messageContent}}, {{createdAt}}, {{createdAt}});

            INSERT INTO live_support_ai_turns
                ("Id", "ConversationId", "SourceMessageId", "PolicyVersionId", "ExpectedConversationVersion", "Status", "ContextCategoryKeysJson", "KnowledgeRevisionIdsJson", "QueuedAt", "Version", "CreatedAt")
            VALUES
                ({{turnId}}, {{conversationId}}, {{messageId}}, {{policyId}}, 1, 2, '[]', '[]', {{createdAt}}, 1, {{createdAt}});

            INSERT INTO live_support_ai_pending_actions
                ("Id", "ConversationId", "TurnId", "StudentUserId", "PolicyVersionId", "ActionKey", "SafeProposalJson", "PayloadHash", "StateFingerprint", "ConfirmationNonceHash", "IdempotencyKey", "Status", "ExpiresAt", "Version", "CreatedAt")
            VALUES
                ({{pendingId}}, {{conversationId}}, {{turnId}}, {{userId}}, {{policyId}}, 'system.handoff', '{}', '', '', '', {{idempotencyKey}}, 0, {{createdAt.AddMinutes(10)}}, 1, {{createdAt}});
            """);

        await migrator.MigrateAsync();
        fixture.Db.ChangeTracker.Clear();

        var message = await fixture.Db.LiveSupportMessages.AsNoTracking().SingleAsync(x => x.Id == messageId);
        var pending = await fixture.Db.LiveSupportAIPendingActions.AsNoTracking().SingleAsync(x => x.Id == pendingId);

        Assert.Equal(messageContent, message.Content);
        Assert.Equal(conversationId, message.ConversationId);
        Assert.Equal(LiveSupportAIPendingDecisionKind.Handoff, pending.DecisionKind);
        Assert.Null(pending.StudentUserId);
        Assert.Equal(LiveSupportAIPendingActionStatus.PendingConfirmation, pending.Status);
        Assert.Equal(idempotencyKey, pending.IdempotencyKey);
    }

    [Fact]
    public async Task New_action_without_protected_payload_is_rejected_by_postgres()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        var userId = await fixture.Db.Users.AsNoTracking().Select(x => x.Id).FirstAsync();
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 99147,
            Status = LiveSupportAIPolicyStatus.Draft,
            SystemInstructions = "test",
            CreatedByUserId = userId,
            Version = 1
        };
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Student,
            StudentUserId = userId,
            LinkedStudentUserId = userId,
            Status = LiveSupportConversationStatus.Waiting,
            Version = 1
        };
        fixture.Db.AddRange(policy, conversation);
        await fixture.Db.SaveChangesAsync();

        var message = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Student,
            SenderUserId = userId,
            ClientMessageId = $"migration-{Guid.NewGuid():N}",
            Type = LiveSupportMessageType.Text,
            Content = "test",
            SentAt = DateTime.UtcNow
        };
        fixture.Db.LiveSupportMessages.Add(message);
        await fixture.Db.SaveChangesAsync();

        var turn = new LiveSupportAITurn
        {
            ConversationId = conversation.Id,
            SourceMessageId = message.Id,
            PolicyVersionId = policy.Id,
            Status = LiveSupportAITurnStatus.Completed,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        fixture.Db.LiveSupportAITurns.Add(turn);
        await fixture.Db.SaveChangesAsync();

        fixture.Db.LiveSupportAIPendingActions.Add(new LiveSupportAIPendingAction
        {
            ConversationId = conversation.Id,
            TurnId = turn.Id,
            StudentUserId = userId,
            PolicyVersionId = policy.Id,
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            ActionKey = "student.lesson.unlock",
            SafeProposalJson = "{}",
            PayloadHash = string.Empty,
            StateFingerprint = string.Empty,
            ConfirmationNonceHash = string.Empty,
            IdempotencyKey = Guid.NewGuid(),
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Version = 1
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }
}
