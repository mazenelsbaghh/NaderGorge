using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class PurchasePaidFundingReader(IAppDbContext db)
{
    private sealed record FundingMetadata(Guid? OperationId, bool HasPaidTeacherAmount);

    public async Task<decimal> ReadAsync(SalesFinancialEffect source, CancellationToken ct)
        => (await ReadAsync([source], ct))[source.PurchaseOperationId];

    public async Task<IReadOnlyDictionary<Guid, decimal>> ReadAsync(
        IReadOnlyCollection<SalesFinancialEffect> sources, CancellationToken ct)
    {
        var paid = sources.ToDictionary(source => source.PurchaseOperationId, source => source.PaidAmount);
        var legacy = sources.Where(source => source.PromotionalAmount > 0m
            && !Metadata(source.DetailsJson).HasPaidTeacherAmount).ToArray();
        if (legacy.Length == 0) return paid;
        var links = await ReadFundingLinks(legacy, ct);
        if (links.Count == 0) return paid;
        await IncludeLegacyPaidRecharge(legacy, links, paid, ct);
        return paid;
    }

    private async Task<Dictionary<Guid, Guid>> ReadFundingLinks(SalesFinancialEffect[] sources, CancellationToken ct)
    {
        var ids = sources.Select(source => source.PurchaseOperationId).ToArray();
        var events = await db.TeacherFinancialEvents.AsNoTracking().Where(financialEvent => ids.Contains(financialEvent.SourceId)
            && (financialEvent.SourceType == TeacherFinancialSourceType.DirectPurchase
                || financialEvent.SourceType == TeacherFinancialSourceType.PublicExamPurchase))
            .Select(financialEvent => new { financialEvent.SourceId, financialEvent.StudentId, financialEvent.TargetId,
                financialEvent.TargetType, financialEvent.PaidAmount, financialEvent.PromotionalAmount, financialEvent.DetailsJson })
            .ToListAsync(ct);
        var links = new Dictionary<Guid, Guid>();
        foreach (var source in sources)
        {
            var candidates = events.Where(financialEvent => financialEvent.SourceId == source.PurchaseOperationId
                    && financialEvent.StudentId == source.StudentId && financialEvent.TargetId == source.TargetId
                    && financialEvent.TargetType == source.TargetType && financialEvent.PaidAmount == source.PaidAmount
                    && financialEvent.PromotionalAmount == source.PromotionalAmount)
                .Select(financialEvent => Metadata(financialEvent.DetailsJson)).ToArray();
            var operationIds = candidates.Where(metadata => !metadata.HasPaidTeacherAmount).Select(metadata => metadata.OperationId)
                .Append(Metadata(source.DetailsJson).OperationId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
            if (operationIds.Length == 1 && !candidates.Any(metadata => metadata.HasPaidTeacherAmount))
                links[source.PurchaseOperationId] = operationIds[0];
        }
        return links;
    }

    private async Task IncludeLegacyPaidRecharge(SalesFinancialEffect[] sources, Dictionary<Guid, Guid> links,
        Dictionary<Guid, decimal> paid, CancellationToken ct)
    {
        var operationIds = links.Values.Distinct().ToArray();
        var usages = await db.PromotionalBalanceUsages.AsNoTracking().Where(usage => operationIds.Contains(usage.PurchaseOperationId))
            .Select(usage => new { usage.PurchaseOperationId, usage.ContentType, usage.ContentId, usage.Amount,
                usage.Allocation.StudentId, usage.Allocation.TeacherId, usage.GiftRecipient.OutcomeCode }).ToListAsync(ct);
        foreach (var source in sources.Where(source => links.ContainsKey(source.PurchaseOperationId)))
        {
            var funding = usages.Where(usage => usage.PurchaseOperationId == links[source.PurchaseOperationId]).ToArray();
            if (funding.Length == 0 || funding.Any(usage => usage.StudentId != source.StudentId
                || usage.ContentId != source.TargetId || usage.ContentType != ContentType(source.TargetType))) continue;
            var total = funding.Sum(usage => usage.Amount);
            var recharge = funding.Where(usage => usage.OutcomeCode == "DIGITAL_RECHARGE"
                && usage.TeacherId == source.TeacherId && source.TeacherId.HasValue).Sum(usage => usage.Amount);
            // Old purchases counted every scoped allocation as a gift. Exact funding links
            // and conservation distinguish those rows from already-corrected sales.
            if (total == source.PromotionalAmount && recharge > 0m && recharge <= total)
                paid[source.PurchaseOperationId] += recharge;
        }
    }

    private static CodeType? ContentType(SalesTargetType target) => target switch
    {
        SalesTargetType.Package => CodeType.Package,
        SalesTargetType.Term => CodeType.Term,
        SalesTargetType.ContentSection => CodeType.Month,
        SalesTargetType.Lesson => CodeType.Lesson,
        SalesTargetType.SpecificVideo or SalesTargetType.VideoType => CodeType.Video,
        SalesTargetType.PublicExam => CodeType.Exam,
        _ => null
    };

    private static FundingMetadata Metadata(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new(null, false);
            var root = document.RootElement;
            Guid? operationId = root.TryGetProperty("fundingOperationId", out var operation)
                && operation.ValueKind == JsonValueKind.String && Guid.TryParse(operation.GetString(), out var id) ? id : null;
            return new(operationId, root.TryGetProperty("paidTeacherBalanceAmount", out _));
        }
        catch (JsonException)
        {
            return new(null, false);
        }
    }
}
