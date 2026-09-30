using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIProposalBuilderTests
{
    [Fact]
    public async Task OrdinarySuggestion_BecomesServerOwnedBoundProposalWithoutExecution()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid();
        var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() }; db.Add(turn); await db.SaveChangesAsync();
        var adapter = new PreviewOnlyAction(); var definition = Definition("ordinary");
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor), new AdminAICapabilityRegistry([definition]), AdminAIStrongConfirmationTests.Protector(), new AdminAISensitiveDataPolicy(), new NoChallenge(), [adapter], new ConfigurationBuilder().AddInMemoryCollection().Build());
        var proposal = await builder.BuildAsync(actor, turn.Id, definition.Key, new { note = "requested" }, default);
        Assert.Equal(AdminAIConfirmationType.Explicit, proposal.Confirmation); Assert.Equal(AdminAIProposalStatus.PendingConfirmation, proposal.Status);
        Assert.Contains("old", System.Text.Json.JsonSerializer.Serialize(proposal.Current)); Assert.Contains("new", System.Text.Json.JsonSerializer.Serialize(proposal.Requested)); Assert.Contains("affected", System.Text.Json.JsonSerializer.Serialize(proposal.Effect));
        Assert.InRange(proposal.ExpiresAt, DateTime.UtcNow.AddSeconds(59), DateTime.UtcNow.AddSeconds(301));
        Assert.Equal(turn.CapabilityBaselineId, db.AdminAIActionProposals.Single().CapabilityBaselineId);
        Assert.Equal(1, adapter.PreviewCalls); Assert.Equal(0, adapter.ExecuteCalls);
    }

    [Fact]
    public async Task UnknownSuggestion_HasZeroPersistenceAndZeroBusinessEffect()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var adapter = new PreviewOnlyAction();
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor), new AdminAICapabilityRegistry([Definition("ordinary")]), AdminAIStrongConfirmationTests.Protector(), new AdminAISensitiveDataPolicy(), new NoChallenge(), [adapter], new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync(actor, Guid.NewGuid(), "unknown", new { }, default));
        Assert.Empty(db.AdminAIActionProposals); Assert.Equal(0, adapter.PreviewCalls); Assert.Equal(0, adapter.ExecuteCalls);
    }

    [Fact]
    public async Task IndependentSuggestions_CreateIndependentProposals()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() }; db.Add(turn); await db.SaveChangesAsync();
        var adapter = new PreviewOnlyAction(); var definition = Definition("ordinary");
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor), new AdminAICapabilityRegistry([definition]), AdminAIStrongConfirmationTests.Protector(), new AdminAISensitiveDataPolicy(), new NoChallenge(), [adapter], new ConfigurationBuilder().Build());
        var proposals = await builder.BuildManyAsync(actor, turn.Id, [new("a", definition.Key, new { value = 1 }), new("b", definition.Key, new { value = 2 })], default);
        Assert.Equal(2, proposals.Count); Assert.Equal(2, proposals.Select(x => x.Id).Distinct().Count()); Assert.Equal(2, db.AdminAIActionProposals.Count()); Assert.Equal(0, adapter.ExecuteCalls);
    }

    [Fact]
    public async Task EveryStrongRisk_IsServerForcedToTypedConfirmation()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() }; db.Add(turn); await db.SaveChangesAsync();
        var definition = Definition("strong"); var adapter = new PreviewOnlyAction(); var protector = AdminAIStrongConfirmationTests.Protector();
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor), new AdminAICapabilityRegistry([definition]), protector, new AdminAISensitiveDataPolicy(), new AdminAIConfirmationChallengeService(db, protector), [adapter], new ConfigurationBuilder().Build());
        var proposal = await builder.BuildAsync(actor, turn.Id, definition.Key, new { }, default);
        Assert.Equal(AdminAIConfirmationType.TypedStrong, proposal.Confirmation); Assert.NotNull(proposal.StrongPhrase); Assert.Equal(AdminAIRiskCategory.Security, proposal.Risk); Assert.Equal(0, adapter.ExecuteCalls);
    }

    [Theory]
    [InlineData("{\"target\":{\"id\":\"not-a-uuid\"}}")]
    [InlineData("{\"target\":{\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\",\"extra\":1}}")]
    [InlineData("{\"target\":{\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\",\"mode\":\"other\"}}")]
    [InlineData("{\"target\":{\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\"},\"count\":5}")]
    [InlineData("{\"target\":{\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\"},\"tags\":[\"same\",\"same\"]}")]
    [InlineData("{\"target\":{\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\"},\"tags\":[\"toolong\"]}")]
    [InlineData("{\"target\":{\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\"},\"tags\":[\"one\",\"two\",\"three\"]}")]
    [InlineData("{\"target\":{\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\",\"id\":\"d70c1e94-60e8-49e0-a8f6-81499079d641\"}}")]
    public async Task NestedActionInput_RejectsInvalidValuesBeforePreview(string inputJson)
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() };
        db.Add(turn); await db.SaveChangesAsync();
        var adapter = new PreviewOnlyAction();
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor),
            new AdminAICapabilityRegistry([Definition("ordinary") with { InputSchema = NestedSchema }]),
            AdminAIStrongConfirmationTests.Protector(), new AdminAISensitiveDataPolicy(), new NoChallenge(),
            [adapter], new ConfigurationBuilder().Build());
        using var input = System.Text.Json.JsonDocument.Parse(inputJson);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildAsync(actor, turn.Id, "test.action", input.RootElement.Clone(), default));

        Assert.Equal(0, adapter.PreviewCalls);
        Assert.Empty(db.AdminAIActionProposals);
    }

    [Fact]
    public async Task NestedActionInput_AcceptsReviewedShape()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() };
        db.Add(turn); await db.SaveChangesAsync();
        var adapter = new PreviewOnlyAction();
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor),
            new AdminAICapabilityRegistry([Definition("ordinary") with { InputSchema = NestedSchema }]),
            AdminAIStrongConfirmationTests.Protector(), new AdminAISensitiveDataPolicy(), new NoChallenge(),
            [adapter], new ConfigurationBuilder().Build());

        var proposal = await builder.BuildAsync(actor, turn.Id, "test.action", new
        {
            target = new { id = "d70c1e94-60e8-49e0-a8f6-81499079d641", mode = "up" },
            count = 2,
            tags = new[] { "one", "two" }
        }, default);

        Assert.Equal(AdminAIProposalStatus.PendingConfirmation, proposal.Status);
        Assert.Equal(1, adapter.PreviewCalls);
    }

    [Theory]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"additionalProperties\":true}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\",\"minimum\":1}},\"additionalProperties\":false}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"additionalProperties\":false,\"oneOf\":[]}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\",\"format\":\"unknown\"}},\"additionalProperties\":false}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\",\"pattern\":\"[\"}},\"additionalProperties\":false}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"},\"value\":{\"type\":\"integer\"}},\"additionalProperties\":false}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"additionalProperties\":false,\"additionalProperties\":false}")]
    public async Task ActionSchema_WithOpenOrIgnoredRules_FailsClosed(string schema)
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() };
        db.Add(turn); await db.SaveChangesAsync();
        var adapter = new PreviewOnlyAction();
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor),
            new AdminAICapabilityRegistry([Definition("ordinary") with { InputSchema = schema }]),
            AdminAIStrongConfirmationTests.Protector(), new AdminAISensitiveDataPolicy(), new NoChallenge(),
            [adapter], new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildAsync(actor, turn.Id, "test.action", new { value = "safe" }, default));

        Assert.Equal(0, adapter.PreviewCalls);
        Assert.Empty(db.AdminAIActionProposals);
    }

    [Fact]
    public async Task ActionSchema_RejectsOpenOptionalNestedObjectEvenWhenOmitted()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        var turn = new AdminAITurn { ActorAdminUserId = actor, ConversationId = Guid.NewGuid(), CapabilityBaselineId = Guid.NewGuid(), SensitiveDataPolicyVersionId = Guid.NewGuid() };
        db.Add(turn); await db.SaveChangesAsync();
        var adapter = new PreviewOnlyAction();
        const string schema = """
            {"type":"object","properties":{"optional":{"type":"object","properties":{},"additionalProperties":true}},"additionalProperties":false}
            """;
        var builder = new AdminAIProposalBuilder(db, new AdminAIConversationTests.AllowAccess(actor),
            new AdminAICapabilityRegistry([Definition("ordinary") with { InputSchema = schema }]),
            AdminAIStrongConfirmationTests.Protector(), new AdminAISensitiveDataPolicy(), new NoChallenge(),
            [adapter], new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildAsync(actor, turn.Id, "test.action", new { }, default));

        Assert.Equal(0, adapter.PreviewCalls);
        Assert.Empty(db.AdminAIActionProposals);
    }

    private const string NestedSchema = """
        {"type":"object","properties":{"target":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"mode":{"type":"string","enum":["up","down"]}},"required":["id"],"additionalProperties":false},"count":{"type":"integer","minimum":1,"maximum":3},"tags":{"type":"array","maxItems":2,"items":{"type":"string","maxLength":5},"uniqueItems":true}},"required":["target"],"additionalProperties":false}
        """;
    private static AdminAICapabilityDefinition Definition(string risk) => new("test.action", "1", "action", risk, risk == "strong" ? "strong" : "ordinary", "{\"type\":\"object\",\"properties\":{\"note\":{\"type\":\"string\"},\"value\":{\"type\":\"integer\"}},\"additionalProperties\":false}", "{}", 1, 4096, 5000, "Fake.Command", ["users"]);
    private sealed class PreviewOnlyAction : IAdminAIActionCapability
    {
        public string Key => "test.action"; public int PreviewCalls { get; private set; } public int ExecuteCalls { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) { PreviewCalls++; return Task.FromResult(new AdminAIActionPreview("user", "user:1", new { note = "old" }, new { note = "new" }, new { affected = 1 }, new { valid = true }, "state-v1")); }
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct) { ExecuteCalls++; throw new InvalidOperationException("Preview must never execute."); }
    }
    private sealed class NoChallenge : IAdminAIConfirmationChallengeService
    { public Task<string> IssueAsync(Guid actorId, Guid proposalId, string label, CancellationToken ct) => throw new InvalidOperationException(); public Task<string?> PhraseAsync(Guid actorId, Guid proposalId, CancellationToken ct) => Task.FromResult<string?>(null); public Task<bool> VerifyAsync(Guid actorId, Guid proposalId, string phrase, CancellationToken ct) => Task.FromResult(false); }
}
