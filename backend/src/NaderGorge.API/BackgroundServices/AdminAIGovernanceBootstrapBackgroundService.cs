using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.BackgroundServices;

public sealed class AdminAIGovernanceBootstrapBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AdminAIGovernanceBootstrapBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("AdminAI:Enabled")) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IAdminAICapabilityRegistry>();
        var policy = scope.ServiceProvider.GetRequiredService<IAdminAISensitiveDataPolicy>();
        RequireReadOnlyRegistry(registry);
        var manifest = BuildManifest(registry);
        var manifestBundle = new GovernanceManifest(
            manifest,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))));
        await ActivateBaselineAsync(db, registry, manifestBundle, stoppingToken);
        await ActivatePolicyAsync(db, policy, stoppingToken);
        await db.SaveChangesAsync(stoppingToken);
        logger.LogInformation("Admin AI governance is active in read-only mode with {CapabilityCount} capabilities.", registry.All.Count);
    }

    private static void RequireReadOnlyRegistry(IAdminAICapabilityRegistry registry)
    {
        if (registry.All.Count == 0 || registry.All.Any(capability => capability.Kind != "read"))
            throw new InvalidOperationException("The automatic Admin AI bootstrap accepts a non-empty read-only catalog only.");
    }

    private static string BuildManifest(IAdminAICapabilityRegistry registry) => JsonSerializer.Serialize(new
    {
        activation = "ready",
        mode = "read-only",
        items = registry.All.Select(capability => new
        {
            key = capability.Key,
            version = capability.Version,
            effect = "read",
            status = "supported"
        })
    });

    private async Task ActivateBaselineAsync(IAppDbContext db, IAdminAICapabilityRegistry registry, GovernanceManifest manifest, CancellationToken cancellationToken)
    {
        var baseline = await db.AdminAICapabilityBaselines.SingleOrDefaultAsync(entry => entry.ManifestHash == manifest.Hash, cancellationToken)
            ?? AddBaseline(db, registry, manifest);
        foreach (var active in await db.AdminAICapabilityBaselines
                     .Where(entry => entry.Status == AdminAICapabilityBaselineStatus.Active && entry.ManifestHash != manifest.Hash)
                     .ToListAsync(cancellationToken))
            active.Status = AdminAICapabilityBaselineStatus.Superseded;
        baseline.Status = AdminAICapabilityBaselineStatus.Active;
    }

    private AdminAICapabilityBaseline AddBaseline(IAppDbContext db, IAdminAICapabilityRegistry registry, GovernanceManifest manifest)
    {
        var baseline = new AdminAICapabilityBaseline
        {
            Version = $"read-{manifest.Hash[..12]}", ManifestHash = manifest.Hash, SafeManifestJson = manifest.Json,
            SourceRevision = configuration["RELEASE_ID"] ?? "runtime", RuntimeInventoryHash = registry.BaselineHash,
            FrontendInventoryHash = registry.BaselineHash, SupportedReadCount = registry.All.Count,
            SupportedActionCount = 0, ExcludedCount = 0, Status = AdminAICapabilityBaselineStatus.Active,
            ApprovedAt = DateTime.UtcNow
        };
        db.AdminAICapabilityBaselines.Add(baseline);
        return baseline;
    }

    private static async Task ActivatePolicyAsync(IAppDbContext db, IAdminAISensitiveDataPolicy policy, CancellationToken cancellationToken)
    {
        var policyVersion = await db.AdminAISensitiveDataPolicyVersions.SingleOrDefaultAsync(entry => entry.PolicyHash == policy.PolicyHash, cancellationToken)
            ?? AddPolicy(db, policy);
        foreach (var active in await db.AdminAISensitiveDataPolicyVersions
                     .Where(entry => entry.Status == AdminAISensitiveDataPolicyStatus.Active && entry.PolicyHash != policy.PolicyHash)
                     .ToListAsync(cancellationToken))
            active.Status = AdminAISensitiveDataPolicyStatus.Superseded;
        policyVersion.Status = AdminAISensitiveDataPolicyStatus.Active;
    }

    private static AdminAISensitiveDataPolicyVersion AddPolicy(IAppDbContext db, IAdminAISensitiveDataPolicy policy)
    {
        var policyVersion = new AdminAISensitiveDataPolicyVersion
        {
            Version = $"policy-{policy.PolicyHash[..12]}", PolicyHash = policy.PolicyHash,
            SafeRulesJson = JsonSerializer.Serialize(new { mode = "closed-schema-redaction", version = 1 }),
            Status = AdminAISensitiveDataPolicyStatus.Active, ApprovedAt = DateTime.UtcNow
        };
        db.AdminAISensitiveDataPolicyVersions.Add(policyVersion);
        return policyVersion;
    }

    private sealed record GovernanceManifest(string Json, string Hash);
}
