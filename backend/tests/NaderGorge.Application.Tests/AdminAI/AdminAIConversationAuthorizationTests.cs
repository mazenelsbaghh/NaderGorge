using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Infrastructure.Services.AdminAI;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIConversationAuthorizationTests
{
    [Fact]
    public async Task EveryConversationQuery_IsOwnerScoped()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-owner-{Guid.NewGuid()}").Options);
        var owner = Guid.NewGuid(); var stranger = Guid.NewGuid();
        var service = new AdminAIConversationService(db, new AdminAIConversationTests.AllowAccess(stranger), AdminAIStrongConfirmationTests.Protector());
        db.AdminAIConversations.Add(new() { OwnerAdminUserId = owner, Title = "private" }); await db.SaveChangesAsync();

        Assert.Empty((await service.ListAsync(stranger, null, null, 20, default)).Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SnapshotAsync(stranger, db.AdminAIConversations.Single().Id, null, 20, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RenameAsync(stranger, db.AdminAIConversations.Single().Id, "x", 1, "rename", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetArchivedAsync(stranger, db.AdminAIConversations.Single().Id, true, 1, "archive", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetArchivedAsync(stranger, db.AdminAIConversations.Single().Id, false, 1, "restore", default));
    }

    [Fact]
    public async Task TurnRoutes_AreOwnerScoped()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-turn-owner-{Guid.NewGuid()}").Options);
        var owner = Guid.NewGuid(); var stranger = Guid.NewGuid();
        var conversation = new AdminAIConversation { OwnerAdminUserId = owner, Title = "private" };
        var turn = new AdminAITurn { Conversation = conversation, ConversationId = conversation.Id, ActorAdminUserId = owner, CallbackIdempotencyDigest = new string('a', 64) };
        db.AddRange(conversation, turn); await db.SaveChangesAsync();
        var orchestrator = new AdminAITurnOrchestrator(db, new AdminAIConversationTests.AllowAccess(stranger), AdminAIStrongConfirmationTests.Protector());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => orchestrator.QueueAsync(stranger, conversation.Id, "question", 1, "queue-key", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => orchestrator.CancelAsync(stranger, conversation.Id, turn.Id, 1, default));
    }

    [Fact]
    public async Task EveryConversationAndTurnOperation_RechecksCurrentAdmin()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-ai-revoked-{Guid.NewGuid()}").Options);
        var actor = Guid.NewGuid(); var denied = new DenyAccess();
        var service = new AdminAIConversationService(db, denied, AdminAIStrongConfirmationTests.Protector());
        var orchestrator = new AdminAITurnOrchestrator(db, denied, AdminAIStrongConfirmationTests.Protector());

        var calls = new Func<Task>[]
        {
            () => service.CreateAsync(actor, null, "key", default),
            () => service.ListAsync(actor, null, null, 20, default),
            () => service.SnapshotAsync(actor, Guid.NewGuid(), null, 20, default),
            () => service.RenameAsync(actor, Guid.NewGuid(), "x", 1, "rename", default),
            () => service.SetArchivedAsync(actor, Guid.NewGuid(), true, 1, "archive", default),
            () => service.SetArchivedAsync(actor, Guid.NewGuid(), false, 1, "restore", default),
            () => orchestrator.QueueAsync(actor, Guid.NewGuid(), "q", 1, "key", default),
            () => orchestrator.CancelAsync(actor, Guid.NewGuid(), Guid.NewGuid(), 1, default)
        };

        foreach (var call in calls) await Assert.ThrowsAsync<UnauthorizedAccessException>(call);
        Assert.Equal(calls.Length, denied.Calls);
    }

    [Fact]
    public void Controller_IsRestrictedToBuiltInAdminRole()
    {
        var authorize = Assert.Single(typeof(AdminAIAgentController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", authorize.Roles);
    }

    private sealed class DenyAccess : NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIAccessGate
    {
        public int Calls { get; private set; }
        public Task<NaderGorge.Application.Features.AdminAI.Interfaces.AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion, CancellationToken ct)
        { Calls++; throw new UnauthorizedAccessException(); }
    }
}
