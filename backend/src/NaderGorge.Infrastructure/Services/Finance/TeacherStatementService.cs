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
            .Include(x => x.TeacherSettlement).ThenInclude(x => x.Lines)
            .Where(x => x.TeacherSettlement.TeacherId == teacherId
                && (!from.HasValue || x.PaidAt >= from.Value)
                && (!to.HasValue || x.PaidAt <= to.Value)).ToListAsync(ct);
        foreach (var payment in settlementPayments)
            rows.Add(new(payment.Id, "SettlementPayment", payment.PaidAt, "صرف تسوية",
                string.Join(" · ", new[] { payment.PaymentMethod }.Concat(payment.TeacherSettlement.Lines
                    .Where(x => x.AllocationId == null && x.AdjustmentId == null && x.Amount < 0m
                        && x.DescriptionSnapshot.StartsWith(TeacherTransferFee.Description, StringComparison.Ordinal))
                    .Select(x => $"{x.DescriptionSnapshot} · الخصم {Money(-x.Amount)}"))),
                payment.TeacherSettlement.Status.ToString(), payment.TransferReference,
                TeacherPaymentAmount: payment.TeacherSettlement.Status == TeacherSettlementStatus.Paid ? payment.Amount : null));

        var adjustments = await db.TeacherPayoutAdjustments.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && (!from.HasValue || x.CreatedAt >= from.Value)
                && (!to.HasValue || x.CreatedAt <= to.Value)).ToListAsync(ct);
        foreach (var adjustment in adjustments)
            rows.Add(new(adjustment.Id, "Adjustment", adjustment.CreatedAt, "تعديل أو مديونية",
                $"{adjustment.Reason} · القيمة {Money(adjustment.Amount)}", adjustment.Status.ToString(), AdjustmentAmount: adjustment.Amount));

        var deliveries = await db.CodeGroupDeliveryConfirmations.AsNoTracking()
            .Include(x => x.CodeGroup).Include(x => x.Payments)
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
                $"{(vodafoneCash ? "تحويل مقبول · فودافون كاش" : "تحويل مقبول")} · {collection.Wallet.Label} · {collection.SenderPhoneNumber} · المبلغ {Money(collection.Amount)} · ليس ربحًا حتى شراء المحتوى",
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
        var transferFees = -settlementPayments.Where(x => x.TeacherSettlement.Status == TeacherSettlementStatus.Paid)
            .SelectMany(x => x.TeacherSettlement.Lines).Where(x => x.AllocationId == null && x.AdjustmentId == null
                && x.Amount < 0m && x.DescriptionSnapshot.StartsWith(TeacherTransferFee.Description, StringComparison.Ordinal)).Sum(x => x.Amount);
        var totals = new TeacherStatementTotals(
            rows.Where(x => x.Kind == "Earning" && x.Recognized).Sum(x => x.TeacherShareAmount ?? 0m),
            rows.Where(x => x.Kind == "Earning" && !x.Recognized && x.Status == "PendingReview").Sum(x => x.TeacherShareAmount ?? 0m),
            rows.Sum(x => x.TeacherPaymentAmount ?? 0m),
            rows.Where(x => x.Kind == "Earning" && x.Recognized && x.RetainedByTeacher).Sum(x => x.TeacherShareAmount ?? 0m),
            rows.Sum(x => x.PlatformDueAmount ?? 0m), rows.Sum(x => x.PlatformPaymentAmount ?? 0m),
            rows.Sum(x => x.StudentCollectionAmount ?? 0m),
            -rows.Where(x => x.Kind == "Adjustment" && x.Status == "Open" && x.AdjustmentAmount < 0m)
                .Sum(x => x.AdjustmentAmount ?? 0m),
            rows.Where(x => x.Kind == "Earning" && x.Recognized).Sum(x => x.PlatformShareAmount ?? 0m) + transferFees, transferFees);
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
        var purchaseIds = purchases.Select(x => x.Id).ToHashSet();
        var purchaseAllocations = allocations.Where(x => purchaseIds.Contains(x.TeacherFinancialEventId)
            && TeacherFinanceAccountService.RecognizedStatuses.Contains(x.ReviewStatus));
        var sales = purchaseAllocations.GroupBy(x => x.TeacherFinancialEventId).Select(group => new {
            Sale = group.First().TeacherFinancialEvent,
            Teacher = group.Sum(x => x.TeacherShareAmount), Platform = group.Sum(x => x.PlatformShareAmount)
        }).GroupBy(x => new { x.Sale.PaidAmount, x.Teacher, x.Platform })
            .Select(group => new TeacherStatementSale(
                group.Where(x => x.Sale.StudentId.HasValue).Select(x => x.Sale.StudentId).Distinct().Count(),
                group.Count(), group.Key.PaidAmount, group.Sum(x => x.Sale.PaidAmount),
                group.Sum(x => x.Teacher), group.Sum(x => x.Platform),
                group.Key.Teacher + group.Key.Platform > 0m
                    ? decimal.Round(100m * group.Key.Platform / (group.Key.Teacher + group.Key.Platform), 2) : null))
            .OrderBy(x => x.UnitPrice).ToArray();
        var batches = deliveries.Select(delivery => {
            var collected = delivery.Payments.Where(x => !to.HasValue || x.ReceivedAt <= to.Value).Sum(x => x.Amount);
            return new TeacherStatementCodeBatch(delivery.CodeGroup.Name, delivery.CodeGroup.TotalCodes,
                delivery.PlatformAmountDue + delivery.TeacherRetainedAmount, delivery.PlatformAmountDue,
                collected, delivery.PlatformAmountDue - collected);
        }).ToArray();
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new TeacherStatement(teacherId, account.TeacherName, from, to, DateTime.UtcNow,
            account, totals, activity, rows, rows.Count, 1, rows.Count, sales, batches);
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
        page.Size(PageSizes.A4);
        page.Margin(30);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(style => style.FontFamily("Tajawal Statement").FontSize(11));
        page.Header().PaddingBottom(16).Column(column =>
        {
            column.Item().Text($"كشف حساب · {statement.TeacherName}").Bold().FontSize(20);
            column.Item().Text($"من {(statement.From.HasValue ? CairoTime.ToLocal(statement.From.Value).ToString("yyyy-MM-dd") : "بداية الحساب")} إلى {(statement.To.HasValue ? CairoTime.ToLocal(statement.To.Value).ToString("yyyy-MM-dd") : "الآن")}");
        });
        page.Content().Column(column =>
        {
            column.Spacing(12);
            column.Item().Text($"اشترى {statement.Activity.PurchasingStudents} طالب · {statement.Activity.PurchaseOperations} عملية · {Money(statement.Activity.PurchaseValue)}").Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                table.Header(header => {
                    foreach (var label in new[] { "كام عملية × السعر", "نصيب المدرس", "نصيب المنصة", "نسبة المنصة*" })
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text(label).Bold();
                });
                foreach (var sale in statement.Sales)
                {
                    Cell($"{sale.Operations} × {Money(sale.UnitPrice)} = {Money(sale.Total)}");
                    Cell(Money(sale.TeacherShare)); Cell(Money(sale.PlatformShare));
                    Cell(sale.PlatformPercent.HasValue ? $"{sale.PlatformPercent:0.##}%" : "—");
                }
                void Cell(string text) => table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(text);
            });
            column.Item().Text("*النسبة الفعلية من المبلغ الموزّع وقت البيع. الطالب قد يشتري أكثر من مرة؛ الباقات المشتركة قد تشمل نصيب مدرس آخر.").FontSize(9);
            column.Item().Text($"المرتجعات: {Money(statement.Activity.RefundAmount)} إلى {statement.Activity.RefundedStudents} طالب");
            column.Item().Background(Colors.Grey.Lighten3).Padding(12).Column(summary => {
                summary.Spacing(6);
                summary.Item().Text($"نصيب المدرس في الفترة بعد المرتجعات: {Money(statement.Totals.Earned)}").Bold();
                summary.Item().Text($"نصيب المنصة في الفترة بعد المرتجعات: {Money(statement.Totals.PlatformEarned)}");
                summary.Item().Text($"دفعت للمدرس في الفترة: {Money(statement.Totals.TeacherPayments)}");
                if (statement.Totals.TransferFees > 0m)
                    summary.Item().Text($"عمولة تحويل فودافون كاش المخصومة من مستحقاته: {Money(statement.Totals.TransferFees)}");
                summary.Item().Text($"نصيبه المحتفظ به من الأكواد: {Money(statement.Totals.RetainedEarnings)}");
                summary.Item().Text($"باقي له الآن ومتاح للصرف: {Money(statement.Account.NetPayable)}").Bold();
                if (statement.Account.Reserved > 0m) summary.Item().Text($"محجوز لصرف لم يكتمل: {Money(statement.Account.Reserved)}");
                if (statement.Account.Debt > 0m) summary.Item().Text($"مديونية حالية: {Money(statement.Account.Debt)}");
            });
            column.Item().Text($"تحويلات الطلاب المقبولة: {statement.Activity.RechargeOperations} تحويل = {Money(statement.Activity.RechargeAmount)}. تشمل القبول اليدوي والمطابقة التلقائية؛ شحن الرصيد لا يُضاف للمبيعات.").FontSize(10);
            column.Item().Text($"الأكواد المستخدمة: {statement.Activity.ActivatedCodes} كود بقيمة {Money(statement.Activity.ActivatedCodeValue)}");
            foreach (var batch in statement.CodeBatches)
                column.Item().Text($"{batch.Name}: سلّمت {batch.Codes} كود · القيمة {(batch.Value.HasValue ? Money(batch.Value.Value) : "غير مسجلة")} · استلمت منه {Money(batch.Collected)} · باقي عليه {(batch.Remaining.HasValue ? Money(batch.Remaining.Value) : "غير مسجل")}");
            column.Item().Text($"إجمالي الباقي على المدرس من الأكواد الآن: {Money(statement.Account.CodeAmountDue)}. منفصل عن المتاح لصرف أرباحه.").FontSize(10);
            var payments = statement.Items.Where(x => x.TeacherPaymentAmount.HasValue).ToArray();
            if (payments.Length > 0)
            {
                column.Item().Text("الفلوس اللي دفعتها للمدرس").Bold();
                foreach (var payment in payments)
                    column.Item().Text($"{CairoTime.ToLocal(payment.OccurredAt):yyyy-MM-dd} · {Money(payment.TeacherPaymentAmount!.Value)} · {payment.Detail}" + (string.IsNullOrWhiteSpace(payment.Reference) ? "" : $" · مرجع {payment.Reference}"));
            }
            if (Math.Abs(statement.Account.SourceDifference) >= 0.01m || Math.Abs(statement.Account.BalanceDifference) >= 0.01m)
                column.Item().Text("يوجد فرق بين رصيد الحساب والحركات المسجلة يحتاج مراجعة قبل الصرف.").Bold();
            column.Item().Text("المبالغ تخص الفترة المختارة، والباقي الآن يشمل كل الفترات. تسليم الأكواد واستخدامها لا يُحسبان مرتين.").FontSize(9);
        });
        page.Footer().AlignCenter().Text(text => { text.Span("صفحة "); text.CurrentPageNumber(); });
    })).GeneratePdf();
}
