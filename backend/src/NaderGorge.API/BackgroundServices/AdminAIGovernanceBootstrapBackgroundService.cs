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
        await AdminAIGovernanceActivationGuard.RequireReviewedBaselineAsync(db, registry, stoppingToken);
        await ActivatePolicyAsync(db, policy, stoppingToken);
        await db.SaveChangesAsync(stoppingToken);
        logger.LogInformation("Admin AI governance uses a reviewed active baseline with {CapabilityCount} capabilities.", registry.All.Count);
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

}
