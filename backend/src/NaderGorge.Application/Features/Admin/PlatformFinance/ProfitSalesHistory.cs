using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance;

public sealed record ProfitMovement(Guid TeacherId, DateTime OccurredAt, decimal Sales,
    decimal TeacherShare, decimal PlatformShare, decimal Refunds);

/// <summary>Rebuilds the profit report from sale funding and cancellation evidence.</summary>
public sealed class ProfitSalesHistory(IAppDbContext db)
{
    // Owner authorized these per-teacher agreements for all earlier sales on 19 September.
    // Resolve the agreement at that fixed instant, so future agreement changes cannot rewrite this decision.
    public static readonly DateTime HistoricalAgreementDate = new(2026, 9, 18, 23, 0, 0, DateTimeKind.Utc);

    public async Task<IReadOnlyList<ProfitMovement>> ReadAsync(DateTime end, CancellationToken ct)
    {
        var events = await db.TeacherFinancialEvents.AsNoTracking().Include(x => x.Allocations)
            .Where(x => x.OccurredAt < end && (x.SourceType == TeacherFinancialSourceType.DirectPurchase
                || x.SourceType == TeacherFinancialSourceType.PublicExamPurchase
                || x.SourceType == TeacherFinancialSourceType.AccessCodeActivation
                || x.SourceType == TeacherFinancialSourceType.AccessCodeGeneration
                || x.SourceType == TeacherFinancialSourceType.SharedPackagePurchase))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);
        var agreements = await db.TeacherFinancialAgreements.AsNoTracking()
            .Where(x => x.EffectiveFrom <= HistoricalAgreementDate
                && (x.EffectiveTo == null || x.EffectiveTo > HistoricalAgreementDate))
            .ToListAsync(ct);
        var funding = await db.PromotionalBalanceUsages.AsNoTracking()
            .Where(x => x.GiftRecipient.OutcomeCode == "DIGITAL_RECHARGE")
            .GroupBy(x => new { x.PurchaseOperationId, x.Allocation.TeacherId })
            .Select(x => new { x.Key.PurchaseOperationId, x.Key.TeacherId, Amount = x.Sum(u => u.Amount) })
            .ToListAsync(ct);
        var cash = funding.ToDictionary(x => (x.PurchaseOperationId, x.TeacherId), x => x.Amount);
        var codeIds = events.Where(x => x.SourceType == TeacherFinancialSourceType.AccessCodeActivation)
            .Select(x => x.SourceId).ToArray();
        var codeTeachers = await db.AccessCodes.AsNoTracking().Where(x => codeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CodeGroup.TeacherId }).ToDictionaryAsync(x => x.Id, x => x.TeacherId, ct);
        var sales = CalculateSales(events, agreements, cash, codeTeachers);
        var movements = sales.Select(x => new ProfitMovement(x.TeacherId, x.Event.OccurredAt,
            x.Paid, x.TeacherShare, x.Paid - x.TeacherShare, 0)).ToList();
        await AppendRefundsAsync(sales, movements, end, ct);
        return movements;
    }

    private static List<SaleState> CalculateSales(IReadOnlyList<TeacherFinancialEvent> events,
        IReadOnlyList<TeacherFinancialAgreement> agreements,
        IReadOnlyDictionary<(Guid, Guid?), decimal> funding, IReadOnlyDictionary<Guid, Guid?> codeTeachers)
    {
        var sales = new List<SaleState>();
        foreach (var sale in events)
        {
            var allocations = sale.Allocations.ToList();
            if (allocations.Count == 0 && codeTeachers.GetValueOrDefault(sale.SourceId) is Guid teacher)
                allocations.Add(new TeacherFinancialAllocation { TeacherId = teacher });
            if (allocations.Count == 0 && sale.PaidAmount != 0m)
                throw new InvalidOperationException("PROFIT_SALE_TEACHER_EVIDENCE_MISSING");
            using var details = JsonDocument.Parse(sale.DetailsJson);
            var root = details.RootElement;
            var alreadyIncludesScopedCash = root.TryGetProperty("paidTeacherBalanceAmount", out _);
            var operation = root.TryGetProperty("fundingOperationId", out var id) && id.TryGetGuid(out var parsed) ? parsed : Guid.Empty;
            foreach (var allocation in allocations)
            {
                var scoped = funding.GetValueOrDefault((operation, (Guid?)allocation.TeacherId));
                var paid = sale.PaidAmount;
                if (allocations.Count > 1)
                {
                    // Shared-package allocations own separate shares, not another copy of the sale.
                    var total = allocations.Sum(x => x.GrossBasisAmount);
                    if (total <= 0 && paid > 0) throw new InvalidOperationException("PROFIT_SHARED_SALE_BASIS_MISSING");
                    paid = total > 0 ? decimal.Round(paid * allocation.GrossBasisAmount / total, 2, MidpointRounding.AwayFromZero) : 0;
                }
                paid += alreadyIncludesScopedCash ? 0m : scoped;
                var terms = HistoricalTerms(sale, allocation, agreements);
                var units = sale.SourceType == TeacherFinancialSourceType.AccessCodeGeneration
                    && root.TryGetProperty("TotalCodes", out var count) ? count.GetInt32() : 1;
                var share = paid <= 0 ? 0 : terms is null ? allocation.TeacherShareAmount
                    : TeacherAgreementResolver.CalculateAllocation(terms, paid, paid, units).TeacherShare;
                sales.Add(new SaleState(sale, allocation.TeacherId, paid, share));
            }
        }
        return sales;
    }

    private static TeacherAgreementResolution? HistoricalTerms(TeacherFinancialEvent sale,
        TeacherFinancialAllocation allocation, IReadOnlyList<TeacherFinancialAgreement> agreements)
    {
        if (sale.OccurredAt >= HistoricalAgreementDate) return null;
        var scope = sale.TargetType switch
        {
            SalesTargetType.Package => TeacherAgreementScopeType.Package,
            SalesTargetType.Term => TeacherAgreementScopeType.Term,
            SalesTargetType.ContentSection => TeacherAgreementScopeType.ContentSection,
            SalesTargetType.Lesson => TeacherAgreementScopeType.Lesson,
            SalesTargetType.SpecificVideo => TeacherAgreementScopeType.LessonVideo,
            SalesTargetType.PublicExam => TeacherAgreementScopeType.PublicExam,
            _ => TeacherAgreementScopeType.SharedPackage
        };
        var trigger = sale.SourceType switch
        {
            TeacherFinancialSourceType.AccessCodeActivation => TeacherAgreementTrigger.CodeActivation,
            TeacherFinancialSourceType.AccessCodeGeneration => TeacherAgreementTrigger.CodeDelivery,
            _ => TeacherAgreementTrigger.ContentSale
        };
        var term = agreements.Where(x => x.TeacherId == allocation.TeacherId && x.Trigger == trigger
                && ((x.ScopeType == scope && (x.ScopeId == null || x.ScopeId == sale.TargetId))
                    || (x.ScopeType == TeacherAgreementScopeType.Default && x.ScopeId == null)))
            .OrderByDescending(x => x.ScopeId.HasValue).ThenByDescending(x => x.ScopeType == scope)
            .ThenByDescending(x => x.EffectiveFrom).FirstOrDefault();
        return term is null ? null : new(term.Id, term.ScopeType, term.ScopeId,
            term.AllocationMode, term.AllocationValue, term.PriceBasis);
    }

    private async Task AppendRefundsAsync(List<SaleState> sales, List<ProfitMovement> movements, DateTime end, CancellationToken ct)
    {
        var audits = await (from audit in db.AuditLogs.AsNoTracking()
            join grant in db.StudentAccessGrants.AsNoTracking() on audit.EntityId equals grant.Id
            where audit.Action == "CANCEL_PACKAGE_GRANT" && audit.CreatedAt < end
            select new { audit.Id, audit.CreatedAt, audit.NewValues, Grant = grant }).ToListAsync(ct);
        var refunds = await db.PlatformRefunds.AsNoTracking().Where(x => x.Status == PlatformRefundStatus.Posted
                && x.JournalEntryId != null)
            .Join(db.JournalEntries.AsNoTracking().Where(x => x.Status == JournalEntryStatus.Posted && x.OccurredAt < end),
                x => x.JournalEntryId, x => (Guid?)x.Id, (refund, journal) => new { Refund = refund, journal.OccurredAt })
            .ToListAsync(ct);
        var actions = new List<RefundAction>();
        foreach (var item in refunds)
            actions.Add(new(item.OccurredAt, item.Refund.StudentId, null, null,
                item.Refund.OriginalSourceId, item.Refund.TotalAmount, item.Refund.TeacherId));
        foreach (var audit in audits)
        {
            using var details = JsonDocument.Parse(audit.NewValues ?? "{}");
            // New posted-refund cancellations identify their original purchase explicitly.
            if (details.RootElement.TryGetProperty("purchaseOperationId", out var purchase) && purchase.ValueKind == JsonValueKind.String)
                continue;
            var grant = audit.Grant;
            var target = grant.GrantType switch
            {
                CodeType.Package => grant.PackageId, CodeType.Term => grant.TermId,
                CodeType.Month => grant.ContentSectionId, CodeType.Lesson => grant.LessonId,
                CodeType.Video => grant.LessonVideoId, CodeType.Exam => grant.PublicExamProductId ?? grant.ExamId,
                _ => null
            };
            if (target is null) throw new InvalidOperationException("PROFIT_CANCELLATION_TARGET_MISSING");
            var amount = details.RootElement.TryGetProperty("refundedAmount", out var refunded) ? refunded.GetDecimal() : 0m;
            actions.Add(new(audit.CreatedAt, grant.UserId, grant.GrantType == CodeType.Exam ? SalesTargetType.PublicExam : (SalesTargetType)(int)grant.GrantType, target,
                null, amount, null));
        }
        await ApplyRefundsAsync(sales, movements, actions, ct);
    }

    private async Task ApplyRefundsAsync(List<SaleState> sales, List<ProfitMovement> movements,
        IEnumerable<RefundAction> actions, CancellationToken ct)
    {
        foreach (var action in actions.OrderBy(x => x.At))
        {
            var matching = sales.Where(x => x.Event.StudentId == action.StudentId && x.Event.OccurredAt <= action.At
                && (action.PurchaseId.HasValue ? x.Event.SourceId == action.PurchaseId
                    : x.Event.TargetType == action.TargetType && x.Event.TargetId == action.TargetId && !x.Cancelled)).ToArray();
            if (matching.Length == 0)
            {
                if (action.Amount > 0)
                {
                    var teacher = action.TeacherId ?? await TargetTeacherAsync(action, ct);
                    if (teacher is null) throw new InvalidOperationException("PROFIT_REFUND_TEACHER_EVIDENCE_MISSING");
                    movements.Add(new(teacher.Value, action.At, 0, 0, -action.Amount, action.Amount));
                }
                continue;
            }
            var paid = matching.Sum(x => x.Paid);
            var fraction = action.PurchaseId.HasValue && paid > 0 ? Math.Min(1m, action.Amount / paid) : 1m;
            var remainingRefund = action.Amount;
            for (var index = 0; index < matching.Length; index++)
            {
                var sale = matching[index];
                var teacher = Math.Min(sale.RemainingTeacher, decimal.Round(sale.TeacherShare * fraction, 2, MidpointRounding.AwayFromZero));
                var refund = index == matching.Length - 1 ? remainingRefund
                    : paid > 0 ? decimal.Round(action.Amount * sale.Paid / paid, 2, MidpointRounding.AwayFromZero) : 0;
                remainingRefund -= refund;
                sale.RemainingTeacher -= teacher;
                sale.Cancelled = !action.PurchaseId.HasValue;
                movements.Add(new(sale.TeacherId, action.At, 0, -teacher, teacher - refund, refund));
            }
        }
    }

    private async Task<Guid?> TargetTeacherAsync(RefundAction refund, CancellationToken ct) => refund.TargetType switch
    {
        SalesTargetType.Package => await db.Packages.Where(x => x.Id == refund.TargetId).Select(x => (Guid?)x.TeacherId).SingleOrDefaultAsync(ct),
        SalesTargetType.Term => await db.Terms.Where(x => x.Id == refund.TargetId).Select(x => (Guid?)x.Package.TeacherId).SingleOrDefaultAsync(ct),
        SalesTargetType.ContentSection => await db.ContentSections.Where(x => x.Id == refund.TargetId).Select(x => (Guid?)x.Term.Package.TeacherId).SingleOrDefaultAsync(ct),
        SalesTargetType.Lesson => await db.Lessons.Where(x => x.Id == refund.TargetId).Select(x => (Guid?)x.ContentSection.Term.Package.TeacherId).SingleOrDefaultAsync(ct),
        SalesTargetType.SpecificVideo => await db.LessonVideos.Where(x => x.Id == refund.TargetId).Select(x => (Guid?)x.Lesson.ContentSection.Term.Package.TeacherId).SingleOrDefaultAsync(ct),
        SalesTargetType.PublicExam => await db.PublicExamProducts.Where(x => x.Id == refund.TargetId).Select(x => (Guid?)x.TeacherId).SingleOrDefaultAsync(ct),
        _ => null
    };

    private sealed class SaleState(TeacherFinancialEvent sale, Guid teacherId, decimal paid, decimal share)
    {
        public TeacherFinancialEvent Event { get; } = sale;
        public Guid TeacherId { get; } = teacherId;
        public decimal Paid { get; } = paid;
        public decimal TeacherShare { get; } = share;
        public decimal RemainingTeacher { get; set; } = share;
        public bool Cancelled { get; set; }
    }
    private sealed record RefundAction(DateTime At, Guid StudentId, SalesTargetType? TargetType,
        Guid? TargetId, Guid? PurchaseId, decimal Amount, Guid? TeacherId);
}
