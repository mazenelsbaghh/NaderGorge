using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using System.Text.Json;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAICapabilityCoverageTests
{
    private static readonly string[] RequiredEvidence =
    [
        "input", "output", "risk", "confirmation", "preview", "execution", "idempotency",
        "concurrency", "audit", "refresh", "security"
    ];

    [Fact]
    public void EmptyProductionRegistry_IsAReleaseBlockerNotCompleteCoverage()
    {
        var report = CoverageReport(new AdminAICapabilityRegistry([]), []);

        Assert.False(report.CanActivate);
        Assert.Equal("CAPABILITY_REGISTRY_EMPTY", report.Blocker);
        Assert.Empty(report.Rows);
    }

    [Fact]
    public void GeneratedBaseline_ReportsEveryMutationAndExternalCandidateAsBlockedUntilItsFullMatrixExists()
    {
        using var baseline = AdminAIBaselineFixture.Open();
        Assert.Equal("blocked", baseline.RootElement.GetProperty("activation").GetString());
        var candidates = baseline.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("effect").GetString() is "mutation" or "external-side-effect")
            .ToArray();

        Assert.NotEmpty(candidates);
        Assert.All(candidates, item =>
        {
            Assert.Equal("blocked", item.GetProperty("status").GetString());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("blocker").GetString()));
            foreach (var field in new[] { "inputSchema", "outputSchema", "risk", "confirmation", "authoritativeOperation", "idempotency", "concurrency", "audit", "refreshScopes" })
                Assert.True(item.TryGetProperty(field, out _), $"{item.GetProperty("id").GetString()} lacks {field}");
        });
    }

    [Fact]
    public void EveryCapabilityReportsEachMissingEvidenceColumn()
    {
        var registry = new AdminAICapabilityRegistry([Action("ordinary.one", "ordinary", "ordinary"), Action("strong.one", "strong", "strong")]);
        var report = CoverageReport(registry, [new Evidence("ordinary.one", ["input", "output"])]);

        Assert.False(report.CanActivate);
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(RequiredEvidence.Except(["input", "output"], StringComparer.Ordinal), report.Rows.Single(x => x.Key == "ordinary.one").Missing);
        Assert.Equal(RequiredEvidence, report.Rows.Single(x => x.Key == "strong.one").Missing);
    }

    [Fact]
    public void DuplicateOrUnknownEvidenceRows_BlockActivation()
    {
        var registry = new AdminAICapabilityRegistry([Action("ordinary.one", "ordinary", "ordinary")]);
        var complete = new Evidence("ordinary.one", RequiredEvidence);

        Assert.Equal("DUPLICATE_EVIDENCE", CoverageReport(registry, [complete, complete]).Blocker);
        Assert.Equal("UNKNOWN_EVIDENCE_KEY", CoverageReport(registry, [complete, new Evidence("unknown", RequiredEvidence)]).Blocker);
    }

    [Fact]
    public void NonEmptyExactMatrix_WithAllRequiredEvidenceCanActivate()
    {
        var registry = new AdminAICapabilityRegistry([Action("ordinary.one", "ordinary", "ordinary")]);
        var report = CoverageReport(registry, [new Evidence("ordinary.one", RequiredEvidence)]);

        Assert.True(report.CanActivate);
        Assert.Null(report.Blocker);
        Assert.Empty(Assert.Single(report.Rows).Missing);
    }

    private static Coverage CoverageReport(IAdminAICapabilityRegistry registry, IReadOnlyList<Evidence> evidence)
    {
        if (registry.All.Count == 0) return new(false, "CAPABILITY_REGISTRY_EMPTY", []);
        if (evidence.GroupBy(x => x.Key, StringComparer.Ordinal).Any(x => x.Count() != 1)) return new(false, "DUPLICATE_EVIDENCE", []);
        var known = registry.All.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        if (evidence.Any(x => !known.Contains(x.Key))) return new(false, "UNKNOWN_EVIDENCE_KEY", []);
        var byKey = evidence.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var rows = registry.All.OrderBy(x => x.Key, StringComparer.Ordinal).Select(definition =>
        {
            var present = byKey.TryGetValue(definition.Key, out var item) ? item.Columns : [];
            return new CoverageRow(definition.Key, RequiredEvidence.Except(present, StringComparer.Ordinal).ToArray());
        }).ToArray();
        return new(rows.All(x => x.Missing.Count == 0), rows.Any(x => x.Missing.Count != 0) ? "MISSING_EVIDENCE" : null, rows);
    }

    private static AdminAICapabilityDefinition Action(string key, string risk, string confirmation) =>
        new(key, "1", "action", risk, confirmation, "closed-input", "safe-output", 0, 4096, 5000, "Command", ["users"]);

    private sealed record Evidence(string Key, IReadOnlyCollection<string> Columns);
    private sealed record CoverageRow(string Key, IReadOnlyList<string> Missing);
    private sealed record Coverage(bool CanActivate, string? Blocker, IReadOnlyList<CoverageRow> Rows);
}

internal static class AdminAIBaselineFixture
{
    public static JsonDocument Open()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var path = Path.Combine(directory.FullName, "tests", "admin_ai_capability_baseline.json");
                if (File.Exists(path)) return JsonDocument.Parse(File.ReadAllText(path));
                directory = directory.Parent;
            }
        }
        throw new FileNotFoundException("tests/admin_ai_capability_baseline.json is required.");
    }
}
