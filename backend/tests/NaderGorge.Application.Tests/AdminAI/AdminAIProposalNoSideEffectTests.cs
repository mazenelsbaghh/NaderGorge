using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIProposalNoSideEffectTests
{
    [Fact]
    public async Task Preview_PersistsOnlyWorkflowRows_AndTouchesNoEffectClient()
    {
        var interceptor = new ChangedEntityInterceptor();
        await using var db = CreateDb(interceptor);
        var actor = Guid.NewGuid();
        var turn = new AdminAITurn
        {
            ActorAdminUserId = actor,
            ConversationId = Guid.NewGuid(),
            CapabilityBaselineId = Guid.NewGuid(),
            SensitiveDataPolicyVersionId = Guid.NewGuid()
        };
        db.Add(turn);
        await db.SaveChangesAsync();
        interceptor.Reset();

        var effects = new FakeEffectClients();
        var adapter = new SideEffectSentinelAction(effects);
        var builder = CreateBuilder(db, actor, adapter);

        await builder.BuildAsync(actor, turn.Id, adapter.Key, new { note = "new" }, default);

        Assert.Equal(1, adapter.PreviewCalls);
        Assert.Equal(0, adapter.ExecuteCalls);
        Assert.Equal(0, effects.TotalCalls);
        Assert.Equal([nameof(AdminAIActionProposal)], interceptor.ChangedTypes.Distinct().Order());
        Assert.Single(db.AdminAIActionProposals);
    }

    [Fact]
    public async Task RejectedInput_HasNoPreview_NoWorkflowWrite_AndNoEffect()
    {
        var interceptor = new ChangedEntityInterceptor();
        await using var db = CreateDb(interceptor);
        var actor = Guid.NewGuid();
        var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() };
        db.Add(turn);
        await db.SaveChangesAsync();
        interceptor.Reset();
        var effects = new FakeEffectClients();
        var adapter = new SideEffectSentinelAction(effects);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateBuilder(db, actor, adapter)
            .BuildAsync(actor, turn.Id, adapter.Key, new { note = "safe", password = "must-not-enter-proposal" }, default));

        Assert.Equal(0, adapter.PreviewCalls);
        Assert.Equal(0, effects.TotalCalls);
        Assert.Empty(interceptor.ChangedTypes);
        Assert.Empty(db.AdminAIActionProposals);
    }

    private static AdminAIProposalBuilder CreateBuilder(AppDbContext db, Guid actor, IAdminAIActionCapability adapter)
    {
        var definition = new AdminAICapabilityDefinition(
            adapter.Key, "1", "action", "ordinary", "ordinary",
            "{\"type\":\"object\",\"additionalProperties\":true}", "{}", 1, 4096, 5000,
            "Fake.AuthoritativeCommand", ["users"]);
        return new AdminAIProposalBuilder(
            db,
            new AdminAIConversationTests.AllowAccess(actor),
            new AdminAICapabilityRegistry([definition]),
            AdminAIStrongConfirmationTests.Protector(),
            new AdminAISensitiveDataPolicy(),
            new NoChallenge(),
            [adapter],
            new ConfigurationBuilder().Build());
    }

    private static AppDbContext CreateDb(ISaveChangesInterceptor interceptor) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-proposal-side-effects-{Guid.NewGuid()}")
            .AddInterceptors(interceptor)
            .Options);

    private sealed class ChangedEntityInterceptor : SaveChangesInterceptor
    {
        public List<string> ChangedTypes { get; } = [];
        public void Reset() => ChangedTypes.Clear();
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context?.ChangeTracker.Entries());
            return result;
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context?.ChangeTracker.Entries());
            return ValueTask.FromResult(result);
        }
        private void Capture(IEnumerable<EntityEntry>? entries)
        {
            if (entries is null) return;
            ChangedTypes.AddRange(entries.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Select(x => x.Entity.GetType().Name));
        }
    }

    private sealed class FakeEffectClients
    {
        public int QueueCalls { get; private set; }
        public int StorageCalls { get; private set; }
        public int ProviderCalls { get; private set; }
        public int MessageCalls { get; private set; }
        public int TotalCalls => QueueCalls + StorageCalls + ProviderCalls + MessageCalls;
    }

    private sealed class SideEffectSentinelAction(FakeEffectClients effects) : IAdminAIActionCapability
    {
        public string Key => "test.side-effect-free-action";
        public int PreviewCalls { get; private set; }
        public int ExecuteCalls { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct)
        {
            PreviewCalls++;
            return Task.FromResult(new AdminAIActionPreview("user", "user:1", new { note = "old" }, new { note = "new" }, new { affected = 1 }, new { valid = true }, new string('a', 64)));
        }
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
        {
            ExecuteCalls++;
            _ = effects;
            throw new InvalidOperationException("Preview must never execute an effect client.");
        }
    }

    private sealed class NoChallenge : IAdminAIConfirmationChallengeService
    {
        public Task<string> IssueAsync(Guid actorId, Guid proposalId, string label, CancellationToken ct) => throw new InvalidOperationException();
        public Task<string?> PhraseAsync(Guid actorId, Guid proposalId, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<bool> VerifyAsync(Guid actorId, Guid proposalId, string phrase, CancellationToken ct) => Task.FromResult(false);
    }
}
