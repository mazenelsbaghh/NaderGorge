using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIConversationTests
{
    [Fact]
    public async Task Lifecycle_PreservesOwnerVersionAndArchiveCancellation()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var service = Service(db, actor);
        var created = await service.CreateAsync(actor, "  اختبار  ", "create-1", default);
        db.AdminAITurns.Add(new AdminAITurn { ConversationId = created.Id, ActorAdminUserId = actor, Status = AdminAITurnStatus.Queued }); await db.SaveChangesAsync();

        var renamed = await service.RenameAsync(actor, created.Id, "عنوان", created.Version, "rename-1", default);
        var archived = await service.SetArchivedAsync(actor, created.Id, true, renamed.Version, "archive-1", default);
        var restored = await service.SetArchivedAsync(actor, created.Id, false, archived.Version, "restore-1", default);

        Assert.Equal("عنوان", renamed.Title); Assert.Equal(AdminAIConversationStatus.Archived, archived.Status);
        Assert.Equal(AdminAIConversationStatus.Active, restored.Status);
        Assert.Equal(AdminAITurnStatus.CancelRequested, db.AdminAITurns.Single().Status);
    }

    [Fact]
    public async Task Snapshot_ReturnsAscendingBoundedMessagesAndCursorList()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var service = Service(db, actor);
        var conversation = await service.CreateAsync(actor, null, "create-1", default);
        await Task.Delay(2);
        await service.CreateAsync(actor, "second", "create-2", default);
        for (var sequence = 1; sequence <= 3; sequence++) db.AdminAIMessages.Add(new AdminAIMessage { ConversationId = conversation.Id, Sequence = sequence, Role = AdminAIMessageRole.Admin, Content = $"m{sequence}" });
        await db.SaveChangesAsync();

        var snapshot = await service.SnapshotAsync(actor, conversation.Id, null, 2, default);
        var page = await service.ListAsync(actor, null, null, 1, default);

        Assert.Equal([2L, 3L], snapshot.Messages.Select(x => x.Sequence)); Assert.True(snapshot.HasOlderMessages);
        Assert.Single(page.Items); Assert.NotNull(page.NextCursor);
        Assert.Single((await service.ListAsync(actor, null, page.NextCursor, 1, default)).Items);
    }

    [Fact]
    public async Task Snapshot_OfNewConversation_ExecutesAgainstRelationalDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var actor = Guid.NewGuid();
        db.Users.Add(new User { Id = actor, FullName = "Admin", PhoneNumber = "01000000000", PasswordHash = "test" });
        await db.SaveChangesAsync();
        var service = Service(db, actor);
        var conversation = await service.CreateAsync(actor, null, "create-relational", default);

        var snapshot = await service.SnapshotAsync(actor, conversation.Id, null, 50, default);

        Assert.Equal(conversation.Id, snapshot.Conversation.Id);
        Assert.Null(snapshot.ActiveTurn);
    }

    [Fact]
    public async Task Snapshot_ReturnsLatestFailedTurnSoClientCanExplainAndRetry()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var service = Service(db, actor);
        var conversation = await service.CreateAsync(actor, null, "create-failed-turn", default);
        var failed = new AdminAITurn
        {
            ConversationId = conversation.Id,
            ActorAdminUserId = actor,
            Status = AdminAITurnStatus.Failed,
            FailureCode = "TOOL_BUDGET_EXCEEDED",
            CompletedAt = DateTime.UtcNow,
        };
        db.AdminAITurns.Add(failed); await db.SaveChangesAsync();

        var snapshot = await service.SnapshotAsync(actor, conversation.Id, null, 50, default);

        Assert.Equal(failed.Id, snapshot.ActiveTurn?.Id);
        Assert.Equal(AdminAITurnStatus.Failed, snapshot.ActiveTurn?.Status);
        Assert.Equal("TOOL_BUDGET_EXCEEDED", snapshot.ActiveTurn?.FailureCode);
    }

    [Fact]
    public async Task StaleVersion_HasNoMutation()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var service = Service(db, actor);
        var conversation = await service.CreateAsync(actor, "original", "create-1", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenameAsync(actor, conversation.Id, "changed", 99, "rename-1", default));
        Assert.Equal("original", db.AdminAIConversations.Single().Title);
    }

    [Fact]
    public async Task Mutations_ReplayDurableReceiptsWithoutRepeatingEffects()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var service = Service(db, actor);
        var conversation = await service.CreateAsync(actor, "original", "create-1", default);
        var renamed = await service.RenameAsync(actor, conversation.Id, "renamed", conversation.Version, "rename-key", default);
        var renameReplay = await service.RenameAsync(actor, conversation.Id, "renamed", conversation.Version, "rename-key", default);
        var archived = await service.SetArchivedAsync(actor, conversation.Id, true, renamed.Version, "archive-key", default);
        var archiveReplay = await service.SetArchivedAsync(actor, conversation.Id, true, renamed.Version, "archive-key", default);

        Assert.Equal(renamed, renameReplay);
        Assert.Equal(archived, archiveReplay);
        Assert.Equal(2, db.AdminAIConversationCommandReceipts.Count());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenameAsync(actor, conversation.Id, "different", conversation.Version, "rename-key", default));
    }

    [Fact]
    public async Task Create_ReplaysSameDurableKey_AndRejectsChangedPayload()
    {
        await using var db = CreateDb(); var actor = Guid.NewGuid(); var service = Service(db, actor);
        var first = await service.CreateAsync(actor, "same", "durable-key", default);
        var replay = await service.CreateAsync(actor, "same", "durable-key", default);

        Assert.Equal(first.Id, replay.Id);
        Assert.Single(db.AdminAIConversations);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(actor, "changed", "durable-key", default));
        Assert.Single(db.AdminAIConversations);
    }

    private static AdminAIConversationService Service(AppDbContext db, Guid actor) =>
        new(db, new AllowAccess(actor), AdminAIStrongConfirmationTests.Protector());

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-conversation-{Guid.NewGuid()}").Options);
    internal sealed class AllowAccess(Guid actor) : IAdminAIAccessGate
    { public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? version, CancellationToken ct) => userId == actor ? Task.FromResult(new AdminAIAccessSnapshot(actor, 1, DateTime.UtcNow)) : throw new UnauthorizedAccessException(); }
}
