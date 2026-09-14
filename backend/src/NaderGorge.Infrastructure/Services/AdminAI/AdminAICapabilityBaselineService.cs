using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed record AdminAIBaselineDraft(
    string Version,
    string ManifestHash,
    string SafeManifestJson,
    string SourceRevision,
    string RuntimeInventoryHash,
    string FrontendInventoryHash,
    int SupportedReadCount,
    int SupportedActionCount,
    int ExcludedCount);

public sealed class AdminAICapabilityBaselineService(IAppDbContext db, IAdminAIAccessGate access)
{
    public async Task<AdminAICapabilityBaseline> CreateDraftAsync(
        Guid actorId,
        AdminAIBaselineDraft draft,
        CancellationToken cancellationToken)
    {
        await access.RequireCurrentAdminAsync(actorId, null, cancellationToken);
        ValidateDraft(draft, requireActivatable: false);
        if (await db.AdminAICapabilityBaselines.AnyAsync(item => item.Version == draft.Version, cancellationToken))
            throw new InvalidOperationException("Admin AI baseline version already exists.");

        var entity = new AdminAICapabilityBaseline
        {
            Version = draft.Version,
            ManifestHash = draft.ManifestHash,
            SafeManifestJson = draft.SafeManifestJson,
            SourceRevision = draft.SourceRevision,
            RuntimeInventoryHash = draft.RuntimeInventoryHash,
            FrontendInventoryHash = draft.FrontendInventoryHash,
            SupportedReadCount = draft.SupportedReadCount,
            SupportedActionCount = draft.SupportedActionCount,
            ExcludedCount = draft.ExcludedCount,
            Status = AdminAICapabilityBaselineStatus.Draft
        };
        db.AdminAICapabilityBaselines.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<AdminAICapabilityBaseline> ActivateAsync(
        Guid actorId,
        Guid baselineId,
        CancellationToken cancellationToken)
    {
        await access.RequireCurrentAdminAsync(actorId, null, cancellationToken);
        var candidate = await db.AdminAICapabilityBaselines.SingleOrDefaultAsync(
            item => item.Id == baselineId,
            cancellationToken) ?? throw new KeyNotFoundException("Admin AI baseline was not found.");
        if (candidate.Status != AdminAICapabilityBaselineStatus.Draft)
            throw new InvalidOperationException("Only a draft Admin AI baseline can be activated.");

        ValidateDraft(new AdminAIBaselineDraft(
            candidate.Version,
            candidate.ManifestHash,
            candidate.SafeManifestJson,
            candidate.SourceRevision,
            candidate.RuntimeInventoryHash,
            candidate.FrontendInventoryHash,
            candidate.SupportedReadCount,
            candidate.SupportedActionCount,
            candidate.ExcludedCount), requireActivatable: true);

        var now = DateTime.UtcNow;
        foreach (var active in await db.AdminAICapabilityBaselines
                     .Where(item => item.Status == AdminAICapabilityBaselineStatus.Active)
                     .ToListAsync(cancellationToken))
            active.Status = AdminAICapabilityBaselineStatus.Superseded;

        candidate.Status = AdminAICapabilityBaselineStatus.Active;
        candidate.ApprovedByAdminUserId = actorId;
        candidate.ApprovedAt = now;

        foreach (var proposal in await db.AdminAIActionProposals
                     .Where(item => item.CapabilityBaselineId != candidate.Id
                         && item.Status == AdminAIProposalStatus.PendingConfirmation)
                     .ToListAsync(cancellationToken))
        {
            proposal.Status = AdminAIProposalStatus.Invalidated;
            proposal.InvalidatedReasonCode = "admin_ai_baseline_superseded";
            proposal.Version++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return candidate;
    }

    private static void ValidateDraft(AdminAIBaselineDraft draft, bool requireActivatable)
    {
        if (string.IsNullOrWhiteSpace(draft.Version) || draft.Version.Length > 100
            || !IsSha256(draft.ManifestHash) || !IsSha256(draft.RuntimeInventoryHash)
            || !IsSha256(draft.FrontendInventoryHash) || string.IsNullOrWhiteSpace(draft.SourceRevision)
            || draft.SupportedReadCount < 0 || draft.SupportedActionCount < 0 || draft.ExcludedCount < 0)
            throw new InvalidOperationException("Admin AI baseline metadata is invalid.");
        if (Encoding.UTF8.GetByteCount(draft.SafeManifestJson) > 4_194_304)
            throw new InvalidOperationException("Admin AI baseline manifest is too large.");

        var actualHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(draft.SafeManifestJson)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash),
                Encoding.ASCII.GetBytes(draft.ManifestHash.ToLowerInvariant())))
            throw new InvalidOperationException("Admin AI baseline manifest hash does not match.");

        using var document = JsonDocument.Parse(draft.SafeManifestJson, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Admin AI baseline manifest contract is invalid.");
        if (!requireActivatable) return;

        if (document.RootElement.TryGetProperty("activation", out var activation)
            && !string.Equals(activation.GetString(), "ready", StringComparison.Ordinal))
            throw new InvalidOperationException("Admin AI baseline is not marked ready for activation.");

        foreach (var item in items.EnumerateArray())
        {
            var effect = item.TryGetProperty("effect", out var effectValue) ? effectValue.GetString() : null;
            var status = item.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
            if (string.Equals(status, "blocked", StringComparison.Ordinal)
                || string.Equals(effect, "mutation", StringComparison.Ordinal)
                    && !string.Equals(status, "supported", StringComparison.Ordinal))
                throw new InvalidOperationException("Admin AI baseline still contains a current business capability gap.");
        }

        if (document.RootElement.TryGetProperty("exclusions", out var exclusions)
            && exclusions.ValueKind == JsonValueKind.Array
            && exclusions.EnumerateArray().Any(IsBusinessExclusion))
            throw new InvalidOperationException("Current Admin business operations cannot be excluded from the baseline.");
    }

    private static bool IsBusinessExclusion(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object
        && ((item.TryGetProperty("isCurrentAdminBusinessMutation", out var current) && current.ValueKind == JsonValueKind.True)
            || (item.TryGetProperty("business", out var business) && business.ValueKind == JsonValueKind.True));

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character));
}
