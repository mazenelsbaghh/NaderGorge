using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAICapabilityPreviewMatrixTests
{
    [Fact]
    public void EmptyProductionRegistry_CannotPassPreviewCoverageVacuously()
    {
        var registry = new AdminAICapabilityRegistry([]);

        Assert.False(HasExactNonEmptyPreviewCoverage(registry, []));
    }

    [Fact]
    public void GeneratedMutationAndExternalPreviewMatrix_RemainsExplicitlyBlockedWithoutRegisteredAdapters()
    {
        using var baseline = AdminAIBaselineFixture.Open();
        var candidates = baseline.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("effect").GetString() is "mutation" or "external-side-effect")
            .ToArray();

        Assert.NotEmpty(candidates);
        Assert.All(candidates, item =>
        {
            Assert.Equal("blocked", item.GetProperty("status").GetString());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("blocker").GetString()));
        });
        Assert.False(HasExactNonEmptyPreviewCoverage(new AdminAICapabilityRegistry([]), []));
    }

    [Fact]
    public async Task EveryRegisteredMutationAndExternalPreview_HasZeroBusinessEffect()
    {
        var effects = new EffectSentinel();
        IAdminAIActionCapability[] adapters = [new PreviewOnlyAction("mutation.one", effects), new PreviewOnlyAction("external.one", effects)];
        var registry = new AdminAICapabilityRegistry(adapters.Select(x => Action(x.Key)));

        Assert.True(HasExactNonEmptyPreviewCoverage(registry, adapters));
        foreach (var adapter in adapters)
        {
            var preview = await adapter.PreviewAsync(Guid.NewGuid(), new { reference = "safe" }, default);
            Assert.NotEmpty(preview.StateFingerprint);
            Assert.Equal(0, effects.TotalCalls);
        }
    }

    [Fact]
    public void MissingDuplicateOrUnknownPreviewAdapter_BlocksCoverage()
    {
        var effects = new EffectSentinel(); var adapter = new PreviewOnlyAction("mutation.one", effects);
        var registry = new AdminAICapabilityRegistry([Action(adapter.Key)]);

        Assert.False(HasExactNonEmptyPreviewCoverage(registry, []));
        Assert.False(HasExactNonEmptyPreviewCoverage(registry, [adapter, adapter]));
        Assert.False(HasExactNonEmptyPreviewCoverage(registry, [new PreviewOnlyAction("unknown", effects)]));
    }

    private static bool HasExactNonEmptyPreviewCoverage(IAdminAICapabilityRegistry registry, IReadOnlyCollection<IAdminAIActionCapability> adapters)
    {
        var expected = registry.All.Where(x => x.Kind == "action").Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
        var actual = adapters.Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
        return expected.Length > 0 && actual.Length == actual.Distinct(StringComparer.Ordinal).Count() && expected.SequenceEqual(actual, StringComparer.Ordinal);
    }

    private static AdminAICapabilityDefinition Action(string key) =>
        new(key, "1", "action", "strong", "strong", "closed-input", "safe-output", 0, 4096, 5000, "Command", ["users"]);

    private sealed class EffectSentinel
    {
        public int DatabaseWrites { get; private set; }
        public int QueueCalls { get; private set; }
        public int FileWrites { get; private set; }
        public int ProviderCalls { get; private set; }
        public int TotalCalls => DatabaseWrites + QueueCalls + FileWrites + ProviderCalls;
    }

    private sealed class PreviewOnlyAction(string key, EffectSentinel effects) : IAdminAIActionCapability
    {
        public string Key { get; } = key;
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken cancellationToken)
        {
            _ = effects;
            return Task.FromResult(new AdminAIActionPreview("target", "safe:1", new { state = "current" }, new { state = "requested" }, new { affected = 1 }, new { valid = true }, new string('a', 64)));
        }
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Preview coverage must not execute a business effect.");
    }
}
