using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using Xunit;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIAdminServiceTests
{
    [Fact]
    public async Task EnableAsync_sets_IsEnabled_true_on_published_policy()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01200000000");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = false,
            SystemInstructions = "Test instructions",
            ReadableDataKeysJson = "[]",
            ActionKeysJson = "[]",
            LookupKeysJson = "[]",
            VerificationQuestionKeysJson = "[]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIAdminService(db, null!);
        var result = await service.EnableAsync(admin.Id, CancellationToken.None);

        Assert.True(result.IsEnabled);
        var updatedPolicy = await db.LiveSupportAIPolicyVersions.FindAsync(policy.Id);
        Assert.True(updatedPolicy!.IsEnabled);
    }

    [Fact]
    public async Task GetStatsAsync_returns_correct_aggregated_metrics()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01200000001");

        // Seed active conversations
        db.LiveSupportAIConversationStates.Add(new LiveSupportAIConversationState
        {
            ConversationId = Guid.NewGuid(),
            Mode = LiveSupportAIMode.AiActive,
            PolicyVersionId = Guid.NewGuid(),
            LastParticipantActivityAt = DateTime.UtcNow
        });

        // Seed resolved AI conversations
        db.LiveSupportAIConversationStates.Add(new LiveSupportAIConversationState
        {
            ConversationId = Guid.NewGuid(),
            Mode = LiveSupportAIMode.AiResolved,
            PolicyVersionId = Guid.NewGuid(),
            LastParticipantActivityAt = DateTime.UtcNow.AddHours(-2),
            ResolvedAt = DateTime.UtcNow.AddHours(-2)
        });

        // Seed handoffs
        db.LiveSupportAIConversationStates.Add(new LiveSupportAIConversationState
        {
            ConversationId = Guid.NewGuid(),
            Mode = LiveSupportAIMode.HumanQueued,
            PolicyVersionId = Guid.NewGuid(),
            LastParticipantActivityAt = DateTime.UtcNow.AddDays(-2),
            HandedOffAt = DateTime.UtcNow.AddDays(-2)
        });

        // Seed AI messages
        db.LiveSupportMessages.Add(new LiveSupportMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            SenderType = LiveSupportSenderType.AI,
            Content = "Hello",
            SentAt = DateTime.UtcNow.AddHours(-5)
        });

        // Seed successful actions
        db.LiveSupportAIPendingActions.Add(new LiveSupportAIPendingAction
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            TurnId = Guid.NewGuid(),
            StudentUserId = Guid.NewGuid(),
            PolicyVersionId = Guid.NewGuid(),
            Status = LiveSupportAIPendingActionStatus.Succeeded,
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        });

        await db.SaveChangesAsync();

        var service = new LiveSupportAIAdminService(db, null!);
        
        // 1. Last 24 Hours
        var stats24h = await service.GetStatsAsync("last-24h", CancellationToken.None);
        Assert.Equal(1, stats24h.ActiveConversations);
        Assert.Equal(1, stats24h.ResolvedIssues);
        Assert.Equal(0, stats24h.Handoffs); // handoff was 2 days ago
        Assert.Equal(1, stats24h.TotalMessagesSent);
        Assert.Equal(1, stats24h.SuccessfulActions);

        // 2. Last 7 Days
        var stats7d = await service.GetStatsAsync("last-7d", CancellationToken.None);
        Assert.Equal(1, stats7d.ActiveConversations);
        Assert.Equal(1, stats7d.ResolvedIssues);
        Assert.Equal(1, stats7d.Handoffs); // handoff was 2 days ago, which is within 7 days
        Assert.Equal(1, stats7d.TotalMessagesSent);
        Assert.Equal(1, stats7d.SuccessfulActions);
    }
}
