using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content;

public sealed class ContentRefundFactSource(IAppDbContext db)
{
    public async Task<HashSet<Guid>> LoadGrantIdsAsync(IReadOnlyList<ContentGrantFact> grants, CancellationToken ct)
    {
        var cancelledIds = grants.Where(grant => grant.CancelledAt.HasValue)
            .Select(grant => grant.GrantId).ToArray();
        if (cancelledIds.Length == 0) return [];

        var audits = await db.AuditLogs.AsNoTracking()
            .Where(audit => audit.EntityType == "StudentAccessGrant" &&
                audit.Action == "CANCEL_PACKAGE_GRANT" && audit.EntityId.HasValue &&
                cancelledIds.Contains(audit.EntityId.Value))
            .Select(audit => new { GrantId = audit.EntityId!.Value, audit.NewValues })
            .ToListAsync(ct);
        var refundedIds = new HashSet<Guid>();
        var purchaseByGrant = new Dictionary<Guid, Guid>();
        foreach (var audit in audits)
        {
            if (string.IsNullOrWhiteSpace(audit.NewValues)) continue;
            using var values = JsonDocument.Parse(audit.NewValues);
            if (values.RootElement.TryGetProperty("refundedAmount", out var amount) &&
                amount.TryGetDecimal(out var refundedAmount) && refundedAmount > 0m)
                refundedIds.Add(audit.GrantId);
            if (values.RootElement.TryGetProperty("purchaseOperationId", out var purchase) &&
                purchase.ValueKind == JsonValueKind.String && purchase.TryGetGuid(out var purchaseId))
                purchaseByGrant[audit.GrantId] = purchaseId;
        }

        var sourceIds = cancelledIds.Concat(purchaseByGrant.Values).Distinct().ToArray();
        var posted = await db.PlatformRefunds.AsNoTracking()
            .Where(refund => refund.Status == PlatformRefundStatus.Posted &&
                sourceIds.Contains(refund.OriginalSourceId))
            .Select(refund => new { refund.OriginalSourceId, refund.OriginalSourceType, refund.StudentId })
            .ToListAsync(ct);
        var postedSources = posted.Select(refund =>
            (refund.OriginalSourceId, refund.OriginalSourceType, refund.StudentId)).ToHashSet();
        foreach (var grant in grants.Where(grant => grant.CancelledAt.HasValue))
        {
            if (postedSources.Contains((grant.GrantId, "HistoricalAccessGrant", grant.UserId)) ||
                (purchaseByGrant.TryGetValue(grant.GrantId, out var purchaseId) &&
                 postedSources.Contains((purchaseId, "PurchaseOperation", grant.UserId))))
                refundedIds.Add(grant.GrantId);
        }
        return refundedIds;
    }
}
