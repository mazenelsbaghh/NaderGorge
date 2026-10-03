using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

public sealed class TeacherDetailedReportService(IAppDbContext db) : ITeacherDetailedReportService
{
    public async Task<FinanceExportResult?> ExportAsync(Guid teacherId, TeacherReportPeriod period, CancellationToken ct)
    {
        var report = await ReadAsync(teacherId, period, ct);
        return report is null ? null : new(TeacherDetailedReportPdf.Generate(report), "application/pdf",
            $"massar-teacher-{teacherId:N}-{period.From?.ToString("yyyy-MM-dd") ?? "beginning"}-{period.To:yyyy-MM-dd}.pdf");
    }

    public async Task<TeacherDetailedReport?> ReadAsync(Guid teacherId, TeacherReportPeriod period, CancellationToken ct)
    {
        if (period.From > period.To || period.To == DateOnly.MaxValue)
            throw new ArgumentException("فترة كشف الحساب غير صالحة", nameof(period));
        var end = CairoTime.GetDayRangeUtc(period.To.ToDateTime(TimeOnly.MinValue)).EndUtc;
        var start = period.From.HasValue ? CairoTime.GetDayRangeUtc(period.From.Value.ToDateTime(TimeOnly.MinValue)).StartUtc : (DateTime?)null;
        await using var snapshot = db is DbContext context && context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            && context.Database.CurrentTransaction is null ? await db.BeginTransactionAsync(
                context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? IsolationLevel.Serializable : IsolationLevel.RepeatableRead, ct) : null;
        var name = await db.TeacherProfiles.AsNoTracking().Where(x => x.Id == teacherId).Select(x => x.User.FullName).SingleOrDefaultAsync(ct);
        if (name is null) return null;
        var sources = await db.TeacherFinancialAllocations.AsNoTracking().Include(x => x.TeacherFinancialEvent)
            .Where(x => x.TeacherId == teacherId && x.TeacherFinancialEvent.OccurredAt < end).ToListAsync(ct);
        var content = await new TeacherReportContentReader(db).ReadAsync(teacherId, ct);
        var grants = await ReadGrants(content, end, ct);
        var allRefunds = await new TeacherReportRefundReader(db).ReadAsync(teacherId, grants, end, ct);
        var allPurchases = BuildPurchases(sources, grants, content, end);
        var purchases = allPurchases.Where(x => InPeriod(x.At, start)).ToArray();
        var payments = await ReadPayments(teacherId, end, ct);
        var adjustments = await ReadAdjustments(teacherId, sources, end, ct);
        var summary = Summarize(sources, payments, adjustments, start);
        var recharges = await ReadRecharges(teacherId, start, end, ct);
        var funding = await new TeacherReportFundingReader(db).ReadAsync(teacherId, end, ct);
        var notes = new List<string>();
        if (sources.Any(x => x.ReviewStatus == TeacherFinancialReviewStatus.PendingReview && InPeriod(x.TeacherFinancialEvent.OccurredAt, start)))
            notes.Add("في عمليات لسه تحت المراجعة. ظاهرة في التفاصيل ومش مضافة للمستحقات المعتمدة.");
        if (purchases.Any(x => x.CancelledAt >= end))
            notes.Add("في مشتريات اتلغت بعد نهاية الفترة؛ مبالغها بتعكس التصحيحات المسجلة وقت استخراج الكشف.");
        if (purchases.Any(x => x.Counted && x.Paid == 0 && (x.Teacher != 0 || x.Platform != 0)))
            notes.Add("في عمليات من غير مبلغ دفع مسجل، لكن عليها توزيع مالي في السجلات. التوزيع ظاهر كما هو ومحتاج مراجعة قبل اعتماد الحساب.");
        if (sources.Any(x => x.TeacherFinancialEvent.SourceType == TeacherFinancialSourceType.SharedPackagePurchase
            && InPeriod(x.TeacherFinancialEvent.OccurredAt, start)))
            notes.Add("الباكدج المشترك ظاهر بسعر الشراء كامل؛ نصيب المستر وعمولتنا هنا تخص المستر ده فقط، مش كل المدرسين في الباكدج.");
        var agreements = await ReadAgreements(teacherId, start, end, ct);
        if (snapshot is not null) await snapshot.CommitAsync(ct);
        return new(name, period, summary, agreements.History, notes,
            purchases, BuildGifts(grants, content, start, end), recharges, funding,
            allRefunds.Where(x => InPeriod(x.At, start)).ToArray(), payments.Where(x => InPeriod(x.At, start)).ToArray(),
            allPurchases.Where(x => x.CancelledAt.HasValue && x.CancelledAt < end && InPeriod(x.CancelledAt.Value, start)).ToArray(),
            BuildMovements(sources, adjustments, start)) { CurrentAgreements = agreements.Current };
    }

    private async Task<List<StudentAccessGrant>> ReadGrants(
        IReadOnlyDictionary<(SalesTargetType, Guid), TeacherReportContent> content, DateTime end, CancellationToken ct)
    {
        Guid[] Ids(SalesTargetType type) => content.Keys.Where(x => x.Item1 == type).Select(x => x.Item2).ToArray();
        var packages = Ids(SalesTargetType.Package); var terms = Ids(SalesTargetType.Term);
        var months = Ids(SalesTargetType.ContentSection); var lessons = Ids(SalesTargetType.Lesson);
        var videos = Ids(SalesTargetType.SpecificVideo); var exams = Ids(SalesTargetType.PublicExam);
        return await db.StudentAccessGrants.AsNoTracking().Include(x => x.User)
            .Where(x => x.GrantedAt < end && x.CreatedAt < end &&
                ((x.PackageId.HasValue && packages.Contains(x.PackageId.Value)) ||
                 (x.TermId.HasValue && terms.Contains(x.TermId.Value)) ||
                 (x.ContentSectionId.HasValue && months.Contains(x.ContentSectionId.Value)) ||
                 (x.LessonId.HasValue && lessons.Contains(x.LessonId.Value)) ||
                 (x.LessonVideoId.HasValue && videos.Contains(x.LessonVideoId.Value)) ||
                 (x.PublicExamProductId.HasValue && exams.Contains(x.PublicExamProductId.Value)))).ToListAsync(ct);
    }

    private static TeacherReportPurchase[] BuildPurchases(List<TeacherFinancialAllocation> sources,
        List<StudentAccessGrant> grants, IReadOnlyDictionary<(SalesTargetType, Guid), TeacherReportContent> content, DateTime end)
    {
        var byCode = grants.Where(x => x.AccessCodeId.HasValue).ToLookup(x => (x.AccessCodeId!.Value, x.UserId));
        var byTarget = grants.Where(x => x.AccessCodeId == null && x.GiftRecipientId == null)
            .ToLookup(x => (x.UserId, TeacherReportContentReader.Target(x)));
        return sources.Where(x => x.TeacherFinancialEvent.SourceType is TeacherFinancialSourceType.DirectPurchase
                or TeacherFinancialSourceType.AccessCodeActivation or TeacherFinancialSourceType.PublicExamPurchase or TeacherFinancialSourceType.SharedPackagePurchase)
            .GroupBy(x => x.TeacherFinancialEventId).Select(group =>
            {
                var sale = group.First().TeacherFinancialEvent;
                var candidates = sale.SourceType == TeacherFinancialSourceType.AccessCodeActivation
                    ? byCode[(sale.SourceId, sale.StudentId ?? Guid.Empty)]
                    : byTarget[(sale.StudentId ?? Guid.Empty, (sale.TargetType, sale.TargetId))];
                var grant = candidates.OrderBy(x => Math.Abs((x.CreatedAt - sale.OccurredAt).TotalSeconds)).FirstOrDefault();
                if (sale.SourceType != TeacherFinancialSourceType.AccessCodeActivation && grant != null
                    && Math.Abs((grant.CreatedAt - sale.OccurredAt).TotalSeconds) > 5) grant = null;
                var target = grant is not null ? TeacherReportContentReader.Target(grant) : (sale.TargetType, sale.TargetId);
                var label = target.HasValue ? content.GetValueOrDefault(target.Value) : null;
                var recognized = group.Where(x => TeacherFinanceAccountService.RecognizedStatuses.Contains(x.ReviewStatus)).ToArray();
                var cancelled = grant?.CancelledAt < end;
                var counted = recognized.Length > 0 && !cancelled;
                var status = cancelled ? "اتلغى" : recognized.Length == 0
                    ? group.Any(x => x.ReviewStatus == TeacherFinancialReviewStatus.PendingReview) ? "تحت المراجعة" : "مرفوض"
                    : sale.PaidAmount == 0 ? "مجاني" : "مدفوع موجود";
                return new TeacherReportPurchase(sale.SourceId, sale.StudentId,
                    grant?.User.FullName ?? group.First().StudentNameSnapshot ?? "طالب غير مسجل",
                    label?.CourseId ?? sale.TargetId, label?.Course ?? group.First().ContentNameSnapshot,
                    label?.Kind ?? PurchaseKind(sale), label?.Name ?? group.First().ContentNameSnapshot,
                    sale.OccurredAt, status, sale.PaidAmount, counted ? recognized.Sum(x => x.TeacherShareAmount) : 0,
                    counted ? recognized.Sum(x => x.PlatformShareAmount) : 0, counted,
                    recognized.Any(x => x.RetainedByTeacher), grant?.Id, grant?.CancelledAt, grant?.CancellationReason);
            }).OrderBy(x => x.Course, StringComparer.Ordinal).ThenBy(x => x.At).ThenBy(x => x.OperationId).ToArray();
    }

    private static string PurchaseKind(TeacherFinancialEvent sale) =>
        sale.SourceType == TeacherFinancialSourceType.SharedPackagePurchase ? "باكدج مشترك" : sale.TargetType switch
        {
            SalesTargetType.Package => "السنة / الباقة", SalesTargetType.Term => "الترم / الكورس",
            SalesTargetType.ContentSection => "الشهر", SalesTargetType.Lesson => "الحصة",
            SalesTargetType.SpecificVideo => "فيديو", SalesTargetType.PublicExam => "امتحان", _ => "محتوى آخر"
        };

    private static TeacherReportGift[] BuildGifts(List<StudentAccessGrant> grants,
        IReadOnlyDictionary<(SalesTargetType, Guid), TeacherReportContent> content, DateTime? start, DateTime end) =>
        grants.Where(x => x.GiftRecipientId.HasValue && InPeriod(x.GrantedAt, start))
            .Select(x => (Grant: x, Target: TeacherReportContentReader.Target(x)))
            .Where(x => x.Target.HasValue && content.ContainsKey(x.Target.Value)).Select(x =>
            {
                var label = content[x.Target!.Value]; var grant = x.Grant;
                return new TeacherReportGift(grant.UserId, grant.User.FullName, label.CourseId, label.Course,
                    label.Kind, label.Name, grant.GrantedAt, grant.CancelledAt < end ? "اتلغى" : "موجود");
            }).OrderBy(x => x.At).ToArray();

    private async Task<TeacherReportPayment[]> ReadPayments(Guid teacherId, DateTime end, CancellationToken ct)
    {
        var payouts = await db.TeacherPayouts.AsNoTracking().Where(x => x.TeacherId == teacherId
            && x.Status == PayoutStatus.Paid && x.PaidAt.HasValue && x.PaidAt < end)
            .Select(x => new TeacherReportPayment(x.Amount, x.PaidAt!.Value, "صرف أرباح", x.TransferReference)).ToListAsync(ct);
        payouts.AddRange(await db.TeacherSettlementPayments.AsNoTracking().Where(x => x.TeacherSettlement.TeacherId == teacherId
            && x.TeacherSettlement.Status == TeacherSettlementStatus.Paid && x.PaidAt < end)
            .Select(x => new TeacherReportPayment(x.Amount, x.PaidAt, x.PaymentMethod, x.TransferReference)).ToListAsync(ct));
        return payouts.OrderBy(x => x.At).ToArray();
    }

    private async Task<List<ReportAdjustment>> ReadAdjustments(Guid teacherId,
        List<TeacherFinancialAllocation> sources, DateTime end, CancellationToken ct)
    {
        var adjustments = await db.TeacherPayoutAdjustments.AsNoTracking().Include(x => x.RelatedFinancialEvent)
            .Where(x => x.TeacherId == teacherId).ToListAsync(ct);
        var debtReversals = sources.Where(x => x.ReviewStatus == TeacherFinancialReviewStatus.Reversed
            && x.PayoutStatus == TeacherFinancialPayoutStatus.Debt && x.TeacherShareAmount < 0)
            .GroupBy(x => (x.TeacherFinancialEvent.StudentId, x.TeacherFinancialEvent.TargetType, x.TeacherFinancialEvent.TargetId))
            .ToDictionary(x => x.Key, x => x.Sum(a => -a.TeacherShareAmount));
        var movements = new List<ReportAdjustment>();
        foreach (var adjustment in adjustments.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
        {
            var at = adjustment.RelatedPayoutId.HasValue ? adjustment.CreatedAt
                : adjustment.RelatedFinancialEvent?.OccurredAt ?? adjustment.CreatedAt;
            var effective = adjustment.Status == TeacherPayoutAdjustmentStatus.Open || adjustment.UpdatedAt >= end;
            if (at >= end || adjustment.Amount >= 0 || !effective) continue;
            // A posted negative earning already reduces entitlement; its debt must not be deducted twice.
            var source = adjustment.RelatedFinancialEvent;
            var key = (source?.StudentId, source?.TargetType ?? SalesTargetType.Platform, source?.TargetId ?? Guid.Empty);
            var duplicate = adjustment.RelatedPayoutId.HasValue ? Math.Min(-adjustment.Amount, debtReversals.GetValueOrDefault(key)) : 0m;
            if (duplicate > 0) debtReversals[key] -= duplicate;
            movements.Add(new(at, adjustment.Amount + duplicate, adjustment.Reason));
        }
        movements.AddRange(await TeacherTransferFee.PaidLines(db).Where(x => x.TeacherSettlement.TeacherId == teacherId
            && x.TeacherSettlement.PaidAt < end).Select(x => new ReportAdjustment(
                x.TeacherSettlement.PaidAt!.Value, x.Amount, x.DescriptionSnapshot, true)).ToListAsync(ct));
        return movements;
    }

    private static TeacherReportSummary Summarize(List<TeacherFinancialAllocation> sources,
        TeacherReportPayment[] payments, List<ReportAdjustment> adjustments, DateTime? start)
    {
        var recognized = sources.Where(x => TeacherFinanceAccountService.RecognizedStatuses.Contains(x.ReviewStatus)).ToArray();
        decimal BalanceBefore(DateTime? boundary) => boundary is null ? 0m :
            recognized.Where(x => x.TeacherFinancialEvent.OccurredAt < boundary).Sum(x => x.RetainedByTeacher ? 0 : x.TeacherShareAmount)
            - payments.Where(x => x.At < boundary).Sum(x => x.Amount) + adjustments.Where(x => x.At < boundary).Sum(x => x.Amount);
        var period = recognized.Where(x => InPeriod(x.TeacherFinancialEvent.OccurredAt, start)).ToArray();
        var opening = BalanceBefore(start); var earned = period.Sum(x => x.TeacherShareAmount);
        var retained = period.Where(x => x.RetainedByTeacher).Sum(x => x.TeacherShareAmount);
        var paid = payments.Where(x => InPeriod(x.At, start)).Sum(x => x.Amount);
        var adjustmentTotal = adjustments.Where(x => InPeriod(x.At, start)).Sum(x => x.Amount);
        var fees = -adjustments.Where(x => InPeriod(x.At, start) && x.IsTransferFee)
            .Sum(x => x.Amount);
        return new(opening, earned, period.Sum(x => x.PlatformShareAmount) + fees, retained, paid, adjustmentTotal + fees,
            opening + earned - retained - paid + adjustmentTotal, fees);
    }

    private static TeacherReportMovement[] BuildMovements(List<TeacherFinancialAllocation> sources,
        List<ReportAdjustment> adjustments, DateTime? start)
    {
        var movements = sources.Where(x => InPeriod(x.TeacherFinancialEvent.OccurredAt, start)
            && TeacherFinanceAccountService.RecognizedStatuses.Contains(x.ReviewStatus)
            && x.TeacherFinancialEvent.SourceType is not (TeacherFinancialSourceType.DirectPurchase
                or TeacherFinancialSourceType.AccessCodeActivation or TeacherFinancialSourceType.PublicExamPurchase
                or TeacherFinancialSourceType.SharedPackagePurchase))
            .Select(x => new TeacherReportMovement(x.TeacherFinancialEvent.OccurredAt,
                x.TeacherFinancialEvent.SourceType switch {
                    TeacherFinancialSourceType.AccessCodeGeneration => "دفعة أكواد · " + x.ContentNameSnapshot,
                    TeacherFinancialSourceType.Cancellation => "إلغاء اشتراك · " + x.ContentNameSnapshot,
                    TeacherFinancialSourceType.Refund => "مرتجع مالي · " + x.ContentNameSnapshot,
                    _ => "حركة حساب · " + x.ContentNameSnapshot },
                x.TeacherShareAmount, x.PlatformShareAmount,
                x.RetainedByTeacher ? "قبضه المستر" : "داخل الحساب",
                x.TeacherFinancialEvent.SourceId.ToString())).ToList();
        movements.AddRange(adjustments.Where(x => x.Amount != 0 && InPeriod(x.At, start))
            .Select(x => new TeacherReportMovement(x.At, x.IsTransferFee
                ? x.Reason : "تسوية / مديونية · " + x.Reason, x.Amount,
                x.IsTransferFee ? -x.Amount : 0m, "داخل الحساب", "")));
        return movements.OrderBy(x => x.At).ToArray();
    }

    private async Task<TeacherReportRecharge[]> ReadRecharges(Guid teacherId, DateTime? start, DateTime end, CancellationToken ct) =>
        await db.RechargeRequests.AsNoTracking().Where(x => x.TeacherId == teacherId
            && (x.Status == RechargeRequestStatus.Approved || x.Status == RechargeRequestStatus.Matched)
            && (x.ResolvedAt ?? x.CreatedAt) < end && (!start.HasValue || (x.ResolvedAt ?? x.CreatedAt) >= start))
            .OrderBy(x => x.ResolvedAt ?? x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new TeacherReportRecharge(x.UserId, x.User.FullName, x.Amount, x.ResolvedAt ?? x.CreatedAt)).ToArrayAsync(ct);

    private async Task<(string[] History, string[] Current)> ReadAgreements(Guid teacherId, DateTime? start, DateTime end, CancellationToken ct)
    {
        var agreements = await db.TeacherFinancialAgreements.AsNoTracking().Where(x => x.TeacherId == teacherId
            && x.EffectiveFrom < end && (!start.HasValue || !x.EffectiveTo.HasValue || x.EffectiveTo >= start))
            .OrderBy(x => x.ScopeType).ThenBy(x => x.EffectiveFrom).ToListAsync(ct);
        var cutoff = end.AddTicks(-1);
        return (agreements.Select(AgreementDescription).Distinct().ToArray(),
            agreements.Where(x => x.IsActive && x.EffectiveFrom <= cutoff && (!x.EffectiveTo.HasValue || x.EffectiveTo > cutoff))
                .Select(AgreementDescription).Distinct().ToArray());
    }

    private static string AgreementDescription(TeacherFinancialAgreement x) => $"{AgreementScope(x.ScopeType)}: {AgreementRule(x)}"
            + (x.ScopeId.HasValue ? " (اتفاق لمحتوى محدد)" : "")
            + $" · من {CairoTime.ToLocal(x.EffectiveFrom):yyyy-MM-dd}"
            + (x.EffectiveTo.HasValue ? $" لحد {CairoTime.ToLocal(x.EffectiveTo.Value):yyyy-MM-dd}" : "");

    private static string AgreementScope(TeacherAgreementScopeType scope) => scope switch
    {
        TeacherAgreementScopeType.Package => "السنة / الباقة", TeacherAgreementScopeType.Term => "الترم / الكورس",
        TeacherAgreementScopeType.ContentSection => "الشهر", TeacherAgreementScopeType.Lesson => "الحصة",
        TeacherAgreementScopeType.LessonVideo => "الفيديو", TeacherAgreementScopeType.CodeGroup => "الأكواد",
        TeacherAgreementScopeType.PublicExam => "الامتحان", TeacherAgreementScopeType.SharedPackage => "الباكدج المشترك", _ => "كل الاشتراكات"
    };

    private static string AgreementRule(TeacherFinancialAgreement agreement) => agreement.AllocationMode switch
    {
        TeacherAgreementAllocationMode.Percentage => $"{100 - agreement.AllocationValue:0.##}% لينا، {agreement.AllocationValue:0.##}% للمستر",
        TeacherAgreementAllocationMode.PlatformFixedPerUnit => $"{agreement.AllocationValue:0.##} جنيه لينا لكل اشتراك",
        TeacherAgreementAllocationMode.FixedPerBatch => $"{agreement.AllocationValue:0.##} جنيه للمستر لكل دفعة",
        _ => $"{agreement.AllocationValue:0.##} جنيه للمستر لكل اشتراك"
    };

    internal static bool InPeriod(DateTime at, DateTime? start) => !start.HasValue || at >= start.Value;
    private sealed record ReportAdjustment(DateTime At, decimal Amount, string Reason, bool IsTransferFee = false);

}
