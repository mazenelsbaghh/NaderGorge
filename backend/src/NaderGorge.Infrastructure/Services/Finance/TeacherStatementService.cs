using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NaderGorge.Infrastructure.Services.Finance;

public sealed class TeacherStatementService(IAppDbContext db) : ITeacherStatementService
{
    private static readonly Lazy<bool> ArabicFont = new(() =>
    {
        using var font = typeof(TeacherStatementService).Assembly.GetManifestResourceStream(
            "NaderGorge.Infrastructure.Assets.Tajawal-Regular.ttf")
            ?? throw new InvalidOperationException("Teacher statement Arabic font is missing");
        FontManager.RegisterFontWithCustomName("Tajawal Statement", font);
        return true;
    });

    public async Task<TeacherStatement?> GetAsync(Guid teacherId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct)
    {
        var statement = await ReadAsync(teacherId, from, to, ct);
        if (statement is null) return null;
        var rows = statement.Items.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return statement with { Items = rows, Page = page, PageSize = pageSize };
    }

    public async Task<FinanceExportResult?> ExportPdfAsync(Guid teacherId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var statement = await ReadAsync(teacherId, from, to, ct);
        if (statement is null) return null;
        _ = ArabicFont.Value;
        QuestPDF.Settings.License = LicenseType.Community;
        var fileName = $"teacher-statement-{teacherId:N}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
        return new FinanceExportResult(BuildPdf(statement), "application/pdf", fileName);
    }

    private async Task<TeacherStatement?> ReadAsync(Guid teacherId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        await using var transaction = db is DbContext context
            && context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            && context.Database.CurrentTransaction is null
            ? await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;

        var account = await new TeacherFinanceAccountService(db).GetAsync(teacherId, ct);
        if (account is null) return null;
        var rows = new List<TeacherStatementRow>();

        var allocations = await db.TeacherFinancialAllocations.AsNoTracking()
            .Include(x => x.TeacherFinancialEvent)
            .Where(x => x.TeacherId == teacherId
                && (!from.HasValue || x.TeacherFinancialEvent.OccurredAt >= from.Value)
                && (!to.HasValue || x.TeacherFinancialEvent.OccurredAt <= to.Value))
            .ToListAsync(ct);
        foreach (var allocation in allocations)
        {
            var sale = allocation.TeacherFinancialEvent;
            var recognized = TeacherFinanceAccountService.RecognizedStatuses.Contains(allocation.ReviewStatus);
            var student = string.IsNullOrWhiteSpace(allocation.StudentNameSnapshot) ? "" : allocation.StudentNameSnapshot;
            var detail = string.Join(" · ", new[] { SourceLabel(sale.SourceType), student, allocation.StudentPhoneSnapshot ?? "",
                $"قيمة العملية {Money(sale.GrossAmount)}", $"خصم {Money(sale.DiscountAmount)}",
                $"المدفوع {Money(sale.PaidAmount)}", $"نصيب المنصة {Money(allocation.PlatformShareAmount)}",
                $"أساس الحساب {Money(allocation.GrossBasisAmount)}",
                $"طريقة الحساب {(allocation.AgreementAllocationMode?.ToString() ?? allocation.AllocationMode.ToString())} ({allocation.AllocationValue:N2})",
                allocation.ReversedAmount == 0m ? "" : $"مرتجع مرتبط بالعملية {Money(allocation.ReversedAmount)} (للتوضيح فقط)" }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            rows.Add(new(allocation.Id, "Earning", sale.OccurredAt,
                allocation.ContentNameSnapshot, detail,
                allocation.ReviewStatus is TeacherFinancialReviewStatus.PendingReview or TeacherFinancialReviewStatus.Rejected
                    ? allocation.ReviewStatus.ToString()
                    : allocation.ReviewStatus == TeacherFinancialReviewStatus.Reversed
                        ? allocation.PayoutStatus == TeacherFinancialPayoutStatus.Debt ? "ReversedDebt" : "Reversed"
                        : allocation.RetainedByTeacher ? "Retained" : allocation.PayoutStatus.ToString(),
                allocation.CodeSerialNumber?.ToString() ?? (sale.SourceId == Guid.Empty ? allocation.Id.ToString("N") : sale.SourceId.ToString("N")),
                sale.GrossAmount, sale.DiscountAmount,
                sale.PaidAmount, allocation.TeacherShareAmount, allocation.PlatformShareAmount,
                Recognized: recognized, RetainedByTeacher: allocation.RetainedByTeacher));
        }

        // A sale can have more than one allocation. Count each teacher-facing sale only once.
        var purchases = allocations
            .Where(x => TeacherFinanceAccountService.RecognizedStatuses.Contains(x.ReviewStatus)
                && x.TeacherFinancialEvent.SourceType is TeacherFinancialSourceType.DirectPurchase
                    or TeacherFinancialSourceType.PublicExamPurchase or TeacherFinancialSourceType.SharedPackagePurchase)
            .GroupBy(x => x.TeacherFinancialEventId)
            .Select(x => x.First().TeacherFinancialEvent).ToArray();

        var payouts = await db.TeacherPayouts.AsNoTracking()
            .Where(x => x.TeacherId == teacherId
                && (!from.HasValue || (x.PaidAt ?? x.CreatedAt) >= from.Value)
                && (!to.HasValue || (x.PaidAt ?? x.CreatedAt) <= to.Value)).ToListAsync(ct);
        foreach (var payout in payouts)
            rows.Add(new(payout.Id, "Payout", payout.PaidAt ?? payout.CreatedAt, "طلب سحب أرباح",
                $"المبلغ المطلوب {Money(payout.Amount)}" + (string.IsNullOrWhiteSpace(payout.RejectionReason ?? payout.AdminNote)
                    ? "" : $" · {payout.RejectionReason ?? payout.AdminNote}"), payout.Status.ToString(),
                payout.TransferReference, TeacherPaymentAmount: payout.Status == PayoutStatus.Paid ? payout.Amount : null));

        var settlements = await db.TeacherSettlements.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && (!from.HasValue || x.CreatedAt >= from.Value)
                && (!to.HasValue || x.CreatedAt <= to.Value)).ToListAsync(ct);
        foreach (var settlement in settlements)
            rows.Add(new(settlement.Id, "Settlement", settlement.CreatedAt, "تسوية أرباح",
                $"الفترة {CairoTime.ToLocal(settlement.PeriodFrom):yyyy-MM-dd} إلى {CairoTime.ToLocal(settlement.PeriodTo):yyyy-MM-dd} · الإجمالي {Money(settlement.GrossDueAmount)} · خصم المديونية {Money(settlement.DebtDeductionAmount)} · صافي التسوية {Money(settlement.NetPayableAmount)}"
                    + (string.IsNullOrWhiteSpace(settlement.Note) ? "" : $" · {settlement.Note}"),
                settlement.Status.ToString()));

        var settlementPayments = await db.TeacherSettlementPayments.AsNoTracking()
            .Include(x => x.TeacherSettlement)
            .Where(x => x.TeacherSettlement.TeacherId == teacherId
                && (!from.HasValue || x.PaidAt >= from.Value)
                && (!to.HasValue || x.PaidAt <= to.Value)).ToListAsync(ct);
        foreach (var payment in settlementPayments)
            rows.Add(new(payment.Id, "SettlementPayment", payment.PaidAt, "صرف تسوية",
                payment.PaymentMethod, payment.TeacherSettlement.Status.ToString(), payment.TransferReference,
                TeacherPaymentAmount: payment.TeacherSettlement.Status == TeacherSettlementStatus.Paid ? payment.Amount : null));

        var adjustments = await db.TeacherPayoutAdjustments.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && (!from.HasValue || x.CreatedAt >= from.Value)
                && (!to.HasValue || x.CreatedAt <= to.Value)).ToListAsync(ct);
        foreach (var adjustment in adjustments)
            rows.Add(new(adjustment.Id, "Adjustment", adjustment.CreatedAt, "تعديل أو مديونية",
                $"{adjustment.Reason} · القيمة {Money(adjustment.Amount)}", adjustment.Status.ToString(), AdjustmentAmount: adjustment.Amount));

        var deliveries = await db.CodeGroupDeliveryConfirmations.AsNoTracking()
            .Include(x => x.CodeGroup)
            .Where(x => x.CodeGroup.TeacherId == teacherId
                && (!from.HasValue || x.ConfirmedAt >= from.Value)
                && (!to.HasValue || x.ConfirmedAt <= to.Value)).ToListAsync(ct);
        foreach (var delivery in deliveries)
            rows.Add(new(delivery.Id, "CodeDelivery", delivery.ConfirmedAt,
                $"تسليم دفعة أكواد: {delivery.CodeGroup.Name}",
                $"المستلم: {delivery.Recipient} · نصيب محتفظ به: {Money(delivery.TeacherRetainedAmount ?? 0m)}",
                "Confirmed", PlatformDueAmount: delivery.PlatformAmountDue));

        var codePayments = await db.CodeGroupDeliveryPayments.AsNoTracking()
            .Include(x => x.DeliveryConfirmation).ThenInclude(x => x.CodeGroup)
            .Where(x => x.DeliveryConfirmation.CodeGroup.TeacherId == teacherId
                && (!from.HasValue || x.ReceivedAt >= from.Value)
                && (!to.HasValue || x.ReceivedAt <= to.Value)).ToListAsync(ct);
        foreach (var payment in codePayments)
            rows.Add(new(payment.Id, "CodePayment", payment.ReceivedAt,
                $"سداد دفعة أكواد: {payment.DeliveryConfirmation.CodeGroup.Name}",
                $"مبلغ استلمته المنصة من المدرس: {Money(payment.Amount)}", "Received", payment.Reference,
                PlatformPaymentAmount: payment.Amount));

        var collections = await db.RechargeRequests.AsNoTracking()
            .Include(x => x.User).Include(x => x.Wallet).Include(x => x.MatchedSmsLog)
            .Where(x => x.TeacherId == teacherId
                && (x.Status == RechargeRequestStatus.Matched || x.Status == RechargeRequestStatus.Approved)
                && (!from.HasValue || (x.ResolvedAt ?? x.CreatedAt) >= from.Value)
                && (!to.HasValue || (x.ResolvedAt ?? x.CreatedAt) <= to.Value)).ToListAsync(ct);
        foreach (var collection in collections)
        {
            var vodafoneCash = IsVodafoneCash(collection.MatchedSmsLog?.Sender);
            rows.Add(new(collection.Id, "StudentCollection", collection.ResolvedAt ?? collection.CreatedAt,
                $"شحن رصيد: {collection.User.FullName}",
                $"{(vodafoneCash ? "فودافون كاش مؤكد" : "مصدر آخر أو غير مؤكد")} · {collection.Wallet.Label} · {collection.SenderPhoneNumber} · المبلغ {Money(collection.Amount)} · ليس ربحًا حتى شراء المحتوى",
                collection.Status.ToString(), collection.MatchedSmsLog?.TransferReference,
                StudentCollectionAmount: collection.Amount));
        }

        // Code usage is read from activation logs, including codes whose teacher share was booked at delivery.
        var codeActivations = await db.AccessCodeActivationLogs.AsNoTracking()
            .Include(x => x.Student).Include(x => x.AccessCode).Include(x => x.Package)
            .Where(x => x.TeacherId == teacherId
                && (!from.HasValue || x.ActivatedAt >= from.Value)
                && (!to.HasValue || x.ActivatedAt <= to.Value)).ToListAsync(ct);
        foreach (var activation in codeActivations)
            rows.Add(new(activation.Id, "CodeActivation", activation.ActivatedAt,
                $"استخدام كود بواسطة {activation.Student.FullName}",
                $"{activation.Package?.Name ?? "محتوى آخر"} · قيمة الكود {Money(activation.Price)} · ربح المدرس من الكود {Money(activation.CommissionEarned)} (محسوب ضمن حركات الأرباح إن وُجد)",
                "Used", activation.AccessCode.SerialNumber.ToString()));

        // Only posted, unreversed refunds represent money actually returned to students.
        var periodFrom = from;
        var periodTo = to;
        var refunds = await (from refund in db.PlatformRefunds.AsNoTracking()
            join journal in db.JournalEntries.AsNoTracking() on refund.JournalEntryId equals (Guid?)journal.Id
            where refund.TeacherId == teacherId && refund.Status == PlatformRefundStatus.Posted
                && (!periodFrom.HasValue || journal.OccurredAt >= periodFrom.Value)
                && (!periodTo.HasValue || journal.OccurredAt <= periodTo.Value)
            select new { Refund = refund, journal.OccurredAt }).ToListAsync(ct);
        var refundStudentIds = refunds.Select(x => x.Refund.StudentId).Distinct().ToArray();
        var refundStudents = await db.Users.AsNoTracking().Where(x => refundStudentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        foreach (var item in refunds)
        {
            var refund = item.Refund;
            rows.Add(new(refund.Id, "StudentRefund", item.OccurredAt,
                $"استرداد للطالب: {refundStudents.GetValueOrDefault(refund.StudentId, "طالب غير معروف")}",
                $"{(refund.Method == PlatformRefundMethod.Cash ? "رد نقدي" : "رد لرصيد الطالب")} · المبلغ {Money(refund.TotalAmount)} · نصيب المدرس المعكوس {Money(refund.TeacherAmount)} · {refund.Reason}",
                "Refunded", refund.PaymentReference, StudentRefundAmount: refund.TotalAmount));
        }

        rows.Sort((left, right) => {
            var byDate = right.OccurredAt.CompareTo(left.OccurredAt);
            return byDate != 0 ? byDate : right.Id.CompareTo(left.Id);
        });
        var totals = new TeacherStatementTotals(
            rows.Where(x => x.Kind == "Earning" && x.Recognized).Sum(x => x.TeacherShareAmount ?? 0m),
            rows.Where(x => x.Kind == "Earning" && !x.Recognized && x.Status == "PendingReview").Sum(x => x.TeacherShareAmount ?? 0m),
            rows.Sum(x => x.TeacherPaymentAmount ?? 0m),
            rows.Where(x => x.Kind == "Earning" && x.Recognized && x.RetainedByTeacher).Sum(x => x.TeacherShareAmount ?? 0m),
            rows.Sum(x => x.PlatformDueAmount ?? 0m), rows.Sum(x => x.PlatformPaymentAmount ?? 0m),
            rows.Sum(x => x.StudentCollectionAmount ?? 0m),
            -rows.Where(x => x.Kind == "Adjustment" && x.Status == "Open" && x.AdjustmentAmount < 0m)
                .Sum(x => x.AdjustmentAmount ?? 0m));
        var vodafoneCollections = collections.Where(x => IsVodafoneCash(x.MatchedSmsLog?.Sender)).ToArray();
        var activity = new TeacherStatementActivity(
            purchases.Where(x => x.StudentId.HasValue).Select(x => x.StudentId!.Value).Distinct().Count(),
            purchases.Length, purchases.Sum(x => x.PaidAmount),
            collections.Select(x => x.UserId).Distinct().Count(), collections.Count,
            collections.Sum(x => x.Amount),
            vodafoneCollections.Select(x => x.UserId).Distinct().Count(), vodafoneCollections.Length,
            vodafoneCollections.Sum(x => x.Amount), collections.Sum(x => x.Amount) - vodafoneCollections.Sum(x => x.Amount),
            refunds.Select(x => x.Refund.StudentId).Distinct().Count(), refunds.Count,
            refunds.Sum(x => x.Refund.TotalAmount),
            codeActivations.Select(x => x.AccessCodeId).Distinct().Count(),
            codeActivations.Select(x => x.StudentId).Distinct().Count(),
            codeActivations.GroupBy(x => x.AccessCodeId).Sum(x => x.First().Price));
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new TeacherStatement(teacherId, account.TeacherName, from, to, DateTime.UtcNow,
            account, totals, activity, rows, rows.Count, 1, rows.Count);
    }

    private static string Money(decimal value) => $"{value:N2} ج.م";

    private static bool IsVodafoneCash(string? sender) =>
        sender?.Trim().Equals("vodafonecash", StringComparison.OrdinalIgnoreCase) == true
        || sender?.Trim().Equals("vf-cash", StringComparison.OrdinalIgnoreCase) == true;

    private static string SourceLabel(TeacherFinancialSourceType source) => source switch
    {
        TeacherFinancialSourceType.AccessCodeActivation => "تفعيل كود",
        TeacherFinancialSourceType.AccessCodeGeneration => "تسليم أكواد",
        TeacherFinancialSourceType.DirectPurchase => "شراء محتوى",
        TeacherFinancialSourceType.PublicExamPurchase => "شراء امتحان",
        TeacherFinancialSourceType.SharedPackagePurchase => "باكدج مشترك",
        TeacherFinancialSourceType.Refund => "مرتجع",
        TeacherFinancialSourceType.Cancellation => "إلغاء",
        TeacherFinancialSourceType.ManualCompensation => "تعويض",
        TeacherFinancialSourceType.ManualAdjustment => "تعديل يدوي",
        _ => source.ToString()
    };

    private static byte[] BuildPdf(TeacherStatement statement) => Document.Create(document => document.Page(page =>
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(22);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(style => style.FontFamily("Tajawal Statement").FontSize(8));
        page.Header().Column(column =>
        {
            column.Item().Text("كشف حساب المدرس").Bold().FontSize(18);
            column.Item().Text($"{statement.TeacherName} · من {(statement.From.HasValue ? CairoTime.ToLocal(statement.From.Value).ToString("yyyy-MM-dd") : "بداية الحساب")} إلى {(statement.To.HasValue ? CairoTime.ToLocal(statement.To.Value).ToString("yyyy-MM-dd") : "تاريخ التقرير")} · صدر {CairoTime.ToLocal(statement.GeneratedAt):yyyy-MM-dd HH:mm}");
            column.Item().PaddingTop(5).Text($"اشترى {statement.Activity.PurchasingStudents} طالب محتوى بقيمة {Money(statement.Activity.PurchaseValue)} ({statement.Activity.PurchaseOperations} عملية) | شحن {statement.Activity.RechargeStudents} طالب {Money(statement.Activity.RechargeAmount)} ({statement.Activity.RechargeOperations} عملية)").Bold();
            column.Item().Text($"فودافون كاش مؤكد: {Money(statement.Activity.VodafoneCashAmount)} من {statement.Activity.VodafoneCashStudents} طالب ({statement.Activity.VodafoneCashOperations} تحويل) | مصادر أخرى أو غير مؤكدة: {Money(statement.Activity.OtherRechargeAmount)}");
            column.Item().Text($"استرد {statement.Activity.RefundedStudents} طالب {Money(statement.Activity.RefundAmount)} ({statement.Activity.RefundOperations} عملية) | استُخدم {statement.Activity.ActivatedCodes} كود بواسطة {statement.Activity.CodeStudents} طالب بقيمة {Money(statement.Activity.ActivatedCodeValue)}");
            column.Item().PaddingTop(5).Text($"أرباح الفترة {Money(statement.Totals.Earned)} | صرف فعلي {Money(statement.Totals.TeacherPayments)} | محتفظ به من الأكواد {Money(statement.Totals.RetainedEarnings)} | متاح للسحب الآن {Money(statement.Account.NetPayable)}").Bold();
            column.Item().Text($"مستحق للمنصة من الأكواد {Money(statement.Totals.PlatformCodeDue)} | سداد الأكواد {Money(statement.Totals.PlatformCodePayments)} | شحن طلاب {Money(statement.Totals.StudentCollections)} | مديونيات مفتوحة بالفترة {Money(statement.Totals.OpenDebtAdjustments)}");
            if (Math.Abs(statement.Account.SourceDifference) >= 0.01m || Math.Abs(statement.Account.BalanceDifference) >= 0.01m)
                column.Item().Text($"فروق تاريخية تحتاج مراجعة: فرق المصادر {Money(statement.Account.SourceDifference)}، فرق الرصيد {Money(statement.Account.BalanceDifference)}. لا تُحتسب كربح إضافي.");
            column.Item().PaddingBottom(6).Text("الطالب محسوب مرة واحدة في كل فئة. فودافون كاش من رسائل التحويل المطابقة فقط. الأرصدة الحالية تخص كل الفترات. شحن الطلاب وقيمة الأكواد ليست أرباحًا إضافية، والمرتجع السالب محسوب مرة واحدة.").FontSize(7);
        });
        page.Content().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.1f); columns.RelativeColumn(1.15f); columns.RelativeColumn(3.8f);
                columns.RelativeColumn(1.1f); columns.RelativeColumn(1f); columns.RelativeColumn(1f);
                columns.RelativeColumn(1f); columns.RelativeColumn(1f);
            });
            table.Header(header =>
            {
                foreach (var label in new[] { "التاريخ", "النوع", "البيان والتفاصيل", "الحالة", "ربح المدرس", "صرف للمدرس", "مستحق للمنصة", "سداد للمنصة" })
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(label).Bold();
            });
            foreach (var row in statement.Items)
            {
                Cell(CairoTime.ToLocal(row.OccurredAt).ToString("yyyy-MM-dd HH:mm"));
                Cell(KindLabel(row.Kind));
                Cell($"{row.Title}\n{row.Detail}" + (string.IsNullOrWhiteSpace(row.Reference) ? "" : $"\nمرجع: {row.Reference}"));
                Cell(StatusLabel(row.Status));
                Cell(row.Kind == "Earning" ? Money(row.TeacherShareAmount ?? 0m) : "—");
                Cell(row.TeacherPaymentAmount.HasValue ? Money(row.TeacherPaymentAmount.Value) : "—");
                Cell(row.PlatformDueAmount.HasValue ? Money(row.PlatformDueAmount.Value) : "—");
                Cell(row.PlatformPaymentAmount.HasValue ? Money(row.PlatformPaymentAmount.Value) : "—");
            }
            void Cell(string value) => table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(4).PaddingHorizontal(3).Text(value);
        });
        page.Footer().AlignCenter().Text(text => { text.Span("صفحة "); text.CurrentPageNumber(); });
    })).GeneratePdf();

    private static string KindLabel(string kind) => kind switch
    {
        "Earning" => "ربح / مرتجع", "Payout" => "طلب سحب", "Settlement" => "تسوية",
        "SettlementPayment" => "صرف تسوية", "Adjustment" => "تعديل / مديونية",
        "CodeDelivery" => "تسليم أكواد", "CodePayment" => "سداد أكواد",
        "StudentCollection" => "شحن طالب", "CodeActivation" => "كود مستخدم",
        "StudentRefund" => "استرداد طالب", _ => kind
    };

    private static string StatusLabel(string status) => status switch
    {
        "AutoApproved" or "Approved" or "Confirmed" => "معتمد", "PendingReview" or "Pending" => "قيد المراجعة",
        "Paid" => "تم الصرف", "Rejected" => "مرفوض", "Reserved" => "محجوز",
        "Retained" => "محتفظ به", "Open" => "مفتوح", "Applied" => "مطبق",
        "Voided" or "Cancelled" => "ملغي", "Received" => "تم السداد", "Matched" => "مطابق",
        "Draft" => "مسودة", "Reviewed" => "تمت المراجعة", "Unpaid" => "غير مصروف",
        "Debt" => "مديونية", "Reversed" => "مرتجع", "ReversedDebt" => "مرتجع / مديونية",
        "Used" => "مستخدم", "Refunded" => "تم الاسترداد", _ => status
    };
}
