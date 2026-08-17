using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAITurnOrchestratorTests
{
    [Fact]
    public async Task Admission_IsAtomicAndDurablyReplayable()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid();
        var conversation = SeedReady(db, actor); await db.SaveChangesAsync();
        var service = Service(db, actor);
        var admissionVersion = conversation.Version;

        var queued = await service.QueueAsync(actor, conversation.Id, "  السؤال  ", admissionVersion, "intent-1", default);
        var replay = await service.QueueAsync(actor, conversation.Id, "السؤال", admissionVersion, "intent-1", default);

        Assert.Equal(queued.Id, replay.Id);
        Assert.Single(db.AdminAITurns);
        Assert.Single(db.AdminAIMessages);
        Assert.Single(db.AdminAITurnSteps);
        Assert.Single(db.OutboxEvents.Where(x => x.Type == "AdminAITurnQueued"));
        Assert.Equal("السؤال", db.AdminAIMessages.Single().Content);
        Assert.Equal(1, conversation.LastSequence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueueAsync(actor, conversation.Id, "different", admissionVersion, "intent-1", default));
    }

    [Fact]
    public async Task Admission_EnforcesConversationAndAdminActiveTurnLimits()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid();
        var first = SeedReady(db, actor);
        var second = new AdminAIConversation { OwnerAdminUserId = actor, Title = "second" };
        var third = new AdminAIConversation { OwnerAdminUserId = actor, Title = "third" };
        db.AddRange(second, third); await db.SaveChangesAsync();
        var service = Service(db, actor);

        await service.QueueAsync(actor, first.Id, "one", first.Version, "one", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueueAsync(actor, first.Id, "duplicate", first.Version + 1, "different", default));
        await service.QueueAsync(actor, second.Id, "two", second.Version, "two", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueueAsync(actor, third.Id, "three", third.Version, "three", default));

        Assert.Equal(2, db.AdminAITurns.Count());
        Assert.Equal(2, db.AdminAIMessages.Count());
        Assert.Equal(2, db.OutboxEvents.Count(x => x.Type == "AdminAITurnQueued"));
    }

    [Fact]
    public async Task Admission_WithMissingContent_ReturnsValidationErrorWithoutPersistingTurn()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid();
        var conversation = SeedReady(db, actor); await db.SaveChangesAsync();
        var service = Service(db, actor);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueueAsync(actor, conversation.Id, null!, conversation.Version, "missing-content", default));

        Assert.Empty(db.AdminAITurns);
        Assert.Empty(db.AdminAIMessages);
        Assert.Empty(db.OutboxEvents);
    }

    [Fact]
    public async Task Cancel_IsVersionedIdempotentAndNeverCancelsForeignTurn()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid();
        var conversation = SeedReady(db, actor); await db.SaveChangesAsync();
        var service = Service(db, actor);
        var queued = await service.QueueAsync(actor, conversation.Id, "one", conversation.Version, "one", default);

        var cancelled = await service.CancelAsync(actor, conversation.Id, queued.Id, queued.Version, default);
        var replay = await service.CancelAsync(actor, conversation.Id, queued.Id, cancelled.Version, default);

        Assert.Equal(AdminAITurnStatus.CancelRequested, replay.Status);
        Assert.NotNull(db.AdminAITurns.Single().CancellationRequestedAt);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CancelAsync(actor, Guid.NewGuid(), queued.Id, replay.Version, default));
    }

    private static AdminAIConversation SeedReady(AppDbContext db, Guid actor)
    {
        var baseline = new AdminAICapabilityBaseline { Version = "v1", Status = AdminAICapabilityBaselineStatus.Active };
        var policy = new AdminAISensitiveDataPolicyVersion { Version = "v1", Status = AdminAISensitiveDataPolicyStatus.Active };
        var conversation = new AdminAIConversation { OwnerAdminUserId = actor, Title = "first" };
        db.AddRange(baseline, policy, conversation);
        return conversation;
    }

    private static AdminAITurnOrchestrator Service(AppDbContext db, Guid actor) =>
        new(db, new AdminAIConversationTests.AllowAccess(actor), AdminAIStrongConfirmationTests.Protector());

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-turns-{Guid.NewGuid()}").Options);
}
