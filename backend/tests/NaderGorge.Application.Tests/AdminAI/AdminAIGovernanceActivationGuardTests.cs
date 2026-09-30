using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIGovernanceActivationGuardTests
{
    [Fact]
    public async Task EnabledReadOnlyCatalog_CannotAutoApproveItself()
    {
        await using var db = CreateDb();
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AdminAIGovernanceActivationGuard.RequireReviewedBaselineAsync(db, registry, default));
        Assert.Empty(db.AdminAICapabilityBaselines);
    }

    [Fact]
    public async Task ApprovedExactReadyBaseline_AllowsStartupWithoutCreatingAnotherBaseline()
    {
        await using var db = CreateDb();
        var registry = Registry();
        var baseline = Baseline(registry);
        db.Add(baseline);
        await db.SaveChangesAsync();

        await AdminAIGovernanceActivationGuard.RequireReviewedBaselineAsync(db, registry, default);

        Assert.Single(db.AdminAICapabilityBaselines);
        Assert.Equal(AdminAICapabilityBaselineStatus.Active, baseline.Status);
    }

    [Fact]
    public async Task TwoActiveBaselines_RejectStartup()
    {
        await using var db = CreateDb();
        var registry = Registry();
        var first = Baseline(registry);
        var second = Baseline(registry);
        second.Version = "second-reviewed-test";
        db.AddRange(first, second);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AdminAIGovernanceActivationGuard.RequireReviewedBaselineAsync(db, registry, default));
    }

    [Theory]
    [InlineData("blocked-item")]
    [InlineData("missing-action")]
    [InlineData("stale-registry")]
    [InlineData("business-exclusion")]
    [InlineData("unapproved")]
    [InlineData("duplicate-field")]
    public async Task IncompleteOrStaleBaseline_FailsClosedBeforeFeatureStarts(string defect)
    {
        await using var db = CreateDb();
        var registry = Registry();
        var baseline = Baseline(registry, defect);
        db.Add(baseline);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AdminAIGovernanceActivationGuard.RequireReviewedBaselineAsync(db, registry, default));

        Assert.Equal(AdminAICapabilityBaselineStatus.Active, baseline.Status);
        Assert.Single(db.AdminAICapabilityBaselines);
    }

    private static AdminAICapabilityRegistry Registry() => new(
    [
        new AdminAICapabilityDefinition("read.one", "1", "read", "read", "none", "{}", "{}", 1, 4096, 5000, "query", []),
        new AdminAICapabilityDefinition("action.one", "1", "action", "ordinary", "ordinary", "{}", "{}", 0, 4096, 5000, "command", ["users"])
    ]);

    private static AdminAICapabilityBaseline Baseline(AdminAICapabilityRegistry registry, string? defect = null)
    {
        var manifest = JsonSerializer.Serialize(new
        {
            activation = "ready",
            registryHash = defect == "stale-registry" ? new string('f', 64) : registry.BaselineHash,
            items = new[] { new { id = "current-action", status = defect == "blocked-item" ? "blocked" : "supported" } },
            capabilities = defect == "missing-action"
                ? new[] { new { key = "read.one", version = "1" } }
                : new[] { new { key = "read.one", version = "1" }, new { key = "action.one", version = "1" } },
            exclusions = defect == "business-exclusion"
                ? new[] { new { isCurrentAdminBusinessMutation = true } }
                : Array.Empty<object>()
        });
        if (defect == "duplicate-field")
            manifest = manifest.Replace("\"activation\":\"ready\"", "\"activation\":\"blocked\",\"activation\":\"ready\"", StringComparison.Ordinal);
        return new AdminAICapabilityBaseline
        {
            Version = "reviewed-test", SourceRevision = "test",
            SafeManifestJson = manifest,
            ManifestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))),
            RuntimeInventoryHash = new string('a', 64), FrontendInventoryHash = new string('b', 64),
            SupportedReadCount = 1, SupportedActionCount = 1, Status = AdminAICapabilityBaselineStatus.Active,
            ApprovedByAdminUserId = defect == "unapproved" ? null : Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow
        };
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-governance-{Guid.NewGuid():N}").Options);
}
