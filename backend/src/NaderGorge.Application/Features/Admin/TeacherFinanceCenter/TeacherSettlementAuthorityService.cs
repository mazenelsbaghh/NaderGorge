using NaderGorge.Application.Interfaces.Finance;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Services;

namespace NaderGorge.Application.Features.Admin.TeacherFinanceCenter;

public sealed record SettlementCreationInput(Guid TeacherId, DateTime PeriodFrom, DateTime PeriodTo,
    string? Note, IReadOnlyList<Guid>? AllocationIds);
public sealed record SettlementPaymentInput(string PaymentMethod, string TransferReference, string? AttachmentUrl, decimal? Amount);
public sealed record ReversalLineInput(Guid AllocationId, decimal Amount);
public sealed record TeacherReversalInput(IReadOnlyList<ReversalLineInput> Lines, string Reason,
    TeacherReversalDisposition Disposition, string IdempotencyKey);

public sealed record SettlementDebtPreview(Guid Id, decimal Amount, string Reason);
public sealed record TeacherSettlementPreview(string? Error, List<TeacherFinancialAllocation> Allocations,
    List<SettlementDebtPreview> Adjustments, decimal GrossDueAmount, decimal DebtDeductionAmount, decimal NetPayableAmount);

public sealed class TeacherSettlementAuthorityService(IAppDbContext db, IFinancialPostingService posting)
{
    public async Task<TeacherSettlementPreview> PreviewAsync(SettlementCreationInput input, CancellationToken ct)
    {
        var preview = await BuildPreviewAsync(input, ct);
        return new(preview.Error, preview.Allocations, preview.Adjustments.Select(x =>
            new SettlementDebtPreview(x.Adjustment.Id, -x.Deduction, x.Adjustment.Reason)).ToList(),
            preview.Gross, preview.Debt, preview.Gross - preview.Debt);
    }

    public async Task<TeacherFinanceCommandResult> CreateAsync(Guid actorId, SettlementCreationInput input, CancellationToken ct)
    {
        if (input.TeacherId == Guid.Empty || input.PeriodTo < input.PeriodFrom)
            return Invalid("بيانات فترة التسوية غير صالحة");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var preview = await BuildPreviewAsync(input, ct);
        if (preview.Error is not null) return Invalid(preview.Error);
        if (preview.Allocations.Count == 0) return Invalid("لا توجد مستحقات مؤهلة لإنشاء تسوية");
        var account = await db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == input.TeacherId, ct);
        var balance = await new TeacherFinanceAccountService(db).GetWithdrawalAsync(input.TeacherId, ct);
        if (account is null || balance.UnreservedBalance < preview.Gross)
            return Conflict("رصيد المعلم المتاح تغير؛ أعد معاينة التسوية");
        if (preview.Gross - preview.Debt > balance.WithdrawalAvailable)
            return Conflict("المتاح للصرف لا يغطي صافي التسوية. أكمل التسوية التي تحجز المديونية أولاً ثم أعد المعاينة.");
        var settlement = NewSettlement(actorId, input, preview);
        ReserveAllocations(settlement, preview);
        account.ReservedBalance += preview.Gross; account.UpdatedAt = DateTime.UtcNow;
        db.TeacherSettlements.Add(settlement); db.FinancialInvoices.Add(NewInvoice(actorId, settlement));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success, settlement.Id);
    }

    public async Task<TeacherFinanceCommandResult> TransitionAsync(Guid actorId, Guid id,
        TeacherSettlementStatus expected, TeacherSettlementStatus next, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settlement = await db.TeacherSettlements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (settlement is null) return NotFound("التسوية غير موجودة");
        if (settlement.Status != expected) return Conflict("انتقال حالة التسوية غير صالح");
        settlement.Status = next;
        if (next == TeacherSettlementStatus.Reviewed) { settlement.ReviewedByUserId = actorId; settlement.ReviewedAt = DateTime.UtcNow; }
        if (next == TeacherSettlementStatus.Approved) { settlement.ApprovedByUserId = actorId; settlement.ApprovedAt = DateTime.UtcNow; }
        var invoice = await db.FinancialInvoices.FirstOrDefaultAsync(x => x.TeacherSettlementId == id, ct);
        if (invoice is not null) invoice.Status = next == TeacherSettlementStatus.Reviewed ? FinancialInvoiceStatus.Reviewed : FinancialInvoiceStatus.Approved;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }

    public async Task<TeacherFinanceCommandResult> PayAsync(Guid actorId, Guid id, SettlementPaymentInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.PaymentMethod) || string.IsNullOrWhiteSpace(input.TransferReference))
            return Invalid("طريقة الدفع والمرجع مطلوبان");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settlement = await db.TeacherSettlements.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (settlement is null) return NotFound("التسوية غير موجودة");
        if (settlement.Status != TeacherSettlementStatus.Approved) return Conflict("يجب اعتماد التسوية قبل تسجيل الدفع");
        if (input.Amount is not null && input.Amount != settlement.NetPayableAmount) return Invalid("يجب أن يطابق مبلغ الدفع صافي التسوية");
        var account = await db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == settlement.TeacherId, ct);
        if (account is null || account.ReservedBalance < settlement.GrossDueAmount || account.CurrentBalance < settlement.GrossDueAmount)
            return Conflict("رصيد التسوية المحجوز غير متاح");
        if (settlement.NetPayableAmount > 0m &&
            !(await new TeacherFinanceAccountService(db).GetWithdrawalAsync(settlement.TeacherId, ct)).CoversReservedPayment)
            return Conflict("المديونية أو الرصيد تغير بعد الحجز. ألغ التسوية وأعد معاينتها قبل الصرف.");
        var allocationIds = settlement.Lines.Where(x => x.AllocationId.HasValue).Select(x => x.AllocationId!.Value).ToList();
        var allocations = await db.TeacherFinancialAllocations.Where(x => allocationIds.Contains(x.Id)).ToListAsync(ct);
        if (allocations.Count != allocationIds.Count || allocations.Any(x => x.PayoutStatus != TeacherFinancialPayoutStatus.Reserved || x.SettlementLineId == null))
            return Conflict("تغيرت حالة بنود التسوية؛ لا يمكن الدفع");
        var adjustmentIds = settlement.Lines.Where(x => x.AdjustmentId.HasValue).Select(x => x.AdjustmentId!.Value).ToList();
        var adjustments = await db.TeacherPayoutAdjustments.Where(x => adjustmentIds.Contains(x.Id)).ToListAsync(ct);
        if (adjustments.Count != adjustmentIds.Count || adjustments.Any(x => x.Status != TeacherPayoutAdjustmentStatus.Open))
            return Conflict("تغيرت حالة مديونية التسوية؛ لا يمكن الدفع");
        var deductions = settlement.Lines.Where(x => x.AdjustmentId.HasValue)
            .GroupBy(x => x.AdjustmentId!.Value).ToDictionary(x => x.Key, x => -x.Sum(line => line.Amount));
        if (adjustments.Any(x => deductions[x.Id] <= 0m || deductions[x.Id] > -x.Amount))
            return Conflict("قيمة خصم المديونية تغيرت؛ أعد معاينة التسوية");
        foreach (var adjustment in adjustments)
        {
            var remaining = -adjustment.Amount - deductions[adjustment.Id];
            if (remaining > 0m)
                db.TeacherPayoutAdjustments.Add(new TeacherPayoutAdjustment
                {
                    Id = Guid.NewGuid(), TeacherId = adjustment.TeacherId,
                    RelatedFinancialEventId = adjustment.RelatedFinancialEventId, RelatedPayoutId = adjustment.RelatedPayoutId,
                    Amount = -remaining, Reason = $"باقي مديونية {adjustment.Id}: {adjustment.Reason}",
                    Status = TeacherPayoutAdjustmentStatus.Open
                });
            adjustment.Status = TeacherPayoutAdjustmentStatus.Applied;
        }
        foreach (var allocation in allocations) allocation.PayoutStatus = TeacherFinancialPayoutStatus.Paid;
        account.CurrentBalance -= settlement.GrossDueAmount; account.ReservedBalance -= settlement.GrossDueAmount; account.UpdatedAt = DateTime.UtcNow;
        settlement.Status = TeacherSettlementStatus.Paid; settlement.PaidByUserId = actorId; settlement.PaidAt = DateTime.UtcNow;
        db.TeacherSettlementPayments.Add(new TeacherSettlementPayment { Id = Guid.NewGuid(), TeacherSettlementId = settlement.Id,
            Amount = settlement.NetPayableAmount, PaymentMethod = input.PaymentMethod.Trim(), TransferReference = input.TransferReference.Trim(),
            AttachmentUrl = input.AttachmentUrl, PaidByUserId = actorId });
        var invoice = await db.FinancialInvoices.FirstOrDefaultAsync(x => x.TeacherSettlementId == settlement.Id, ct);
        if (invoice is not null) { invoice.Status = FinancialInvoiceStatus.Paid; invoice.PaymentReference = input.TransferReference.Trim(); invoice.AttachmentUrl = input.AttachmentUrl; }
        if (settlement.NetPayableAmount > 0m)
            await posting.PostAsync(new FinancialPostingRequest("TeacherSettlement", settlement.Id,
                "TeacherSettlement", $"teacher-settlement:{settlement.Id:N}:paid", "صرف تسوية مدرس",
                settlement.PaidAt.Value, actorId,
                [new("2000", settlement.NetPayableAmount, 0m, TeacherId: settlement.TeacherId),
                 new("1000", 0m, settlement.NetPayableAmount, TeacherId: settlement.TeacherId)]), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }

    public async Task<TeacherFinanceCommandResult> CancelAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settlement = await db.TeacherSettlements.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (settlement is null) return NotFound("التسوية غير موجودة");
        if (settlement.Status == TeacherSettlementStatus.Paid) return Conflict("لا يمكن إلغاء تسوية مدفوعة");
        if (settlement.Status == TeacherSettlementStatus.Cancelled) return new(TeacherFinanceCommandStatus.Success, AlreadyApplied: true);
        var account = await db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == settlement.TeacherId, ct);
        if (account is null || account.ReservedBalance < settlement.GrossDueAmount) return Conflict("الرصيد المحجوز غير متاح");
        var ids = settlement.Lines.Where(x => x.AllocationId.HasValue).Select(x => x.AllocationId!.Value).ToList();
        var allocations = await db.TeacherFinancialAllocations.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (allocations.Any(x => x.PayoutStatus != TeacherFinancialPayoutStatus.Reserved)) return Conflict("بعض البنود لم تعد قابلة للتحرير");
        foreach (var allocation in allocations) { allocation.PayoutStatus = TeacherFinancialPayoutStatus.Unpaid; allocation.SettlementLineId = null; }
        account.ReservedBalance -= settlement.GrossDueAmount; account.UpdatedAt = DateTime.UtcNow; settlement.Status = TeacherSettlementStatus.Cancelled;
        var invoice = await db.FinancialInvoices.FirstOrDefaultAsync(x => x.TeacherSettlementId == settlement.Id, ct);
        if (invoice is not null) invoice.Status = FinancialInvoiceStatus.Cancelled;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }

    public async Task<TeacherFinanceCommandResult> AttachInvoiceAsync(Guid id, string attachmentUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(attachmentUrl)) return Invalid("رابط المرفق مطلوب");
        var invoice = await db.FinancialInvoices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (invoice is null) return NotFound("الفاتورة غير موجودة");
        if (invoice.Status == FinancialInvoiceStatus.Cancelled) return Conflict("لا يمكن تعديل فاتورة ملغاة");
        invoice.AttachmentUrl = attachmentUrl.Trim(); await db.SaveChangesAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }

    public async Task<TeacherFinanceCommandResult> ReverseAsync(TeacherReversalInput input, CancellationToken ct)
    {
        if (input.Lines.Count == 0 || string.IsNullOrWhiteSpace(input.Reason) || string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
            input.Lines.Any(x => x.AllocationId == Guid.Empty || x.Amount <= 0m)) return Invalid("بيانات المرتجع غير صالحة");
        if (input.Disposition == TeacherReversalDisposition.ReverseAvailableBalance) return Invalid("اختر مديونية المدرس أو خصم التسوية القادمة");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var duplicate = await db.TeacherFinancialEvents.FirstOrDefaultAsync(x => x.IdempotencyKey == input.IdempotencyKey, ct);
        if (duplicate is not null) return new(TeacherFinanceCommandStatus.Success, duplicate.Id, AlreadyApplied: true);
        var ids = input.Lines.Select(x => x.AllocationId).Distinct().ToList();
        if (ids.Count != input.Lines.Count) return Invalid("لا يمكن تكرار بند في نفس المرتجع");
        var allocations = await db.TeacherFinancialAllocations.Include(x => x.TeacherFinancialEvent).Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (allocations.Count != ids.Count || allocations.Any(x => x.TeacherShareAmount <= 0m || x.PayoutStatus is TeacherFinancialPayoutStatus.Reversed or TeacherFinancialPayoutStatus.Debt or TeacherFinancialPayoutStatus.Reserved)) return Conflict("أحد بنود المرتجع غير متاح");
        if (allocations.Select(x => x.TeacherId).Distinct().Count() != 1) return Invalid("يجب أن تخص البنود مدرساً واحداً");
        var amounts = input.Lines.ToDictionary(x => x.AllocationId, x => x.Amount);
        if (allocations.Any(x => amounts[x.Id] > x.TeacherShareAmount - x.ReversedAmount)) return Conflict("قيمة المرتجع تتجاوز الرصيد القابل للعكس");
        var teacherId = allocations[0].TeacherId;
        var available = allocations.Where(x => x.PayoutStatus != TeacherFinancialPayoutStatus.Paid).Sum(x => amounts[x.Id]);
        var account = available == 0m ? null : await db.TeacherAccounts.FirstOrDefaultAsync(x => x.TeacherId == teacherId, ct);
        if (available > 0m && (account is null || account.CurrentBalance - account.ReservedBalance < available)) return Conflict("الرصيد المتاح تغير؛ أعد العملية");
        var reversal = NewReversal(input, allocations, amounts);
        ApplyReversal(input, allocations, amounts, account, reversal);
        db.TeacherFinancialEvents.Add(reversal); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success, reversal.Id);
    }

    private async Task<SettlementPreviewState> BuildPreviewAsync(SettlementCreationInput input, CancellationToken ct)
    {
        if (input.PeriodTo < input.PeriodFrom || !await db.TeacherProfiles.AnyAsync(x => x.Id == input.TeacherId, ct)) return SettlementPreviewState.Invalid("بيانات التسوية أو المدرس غير صالحة");
        var requested = input.AllocationIds?.Distinct().ToList();
        if (input.AllocationIds is not null && requested!.Count != input.AllocationIds.Count) return SettlementPreviewState.Invalid("لا يمكن اختيار بند مرتين");
        var query = db.TeacherFinancialAllocations.Include(x => x.TeacherFinancialEvent).Where(x => x.TeacherId == input.TeacherId && x.TeacherShareAmount > x.ReversedAmount && x.PayoutStatus == TeacherFinancialPayoutStatus.Unpaid && (x.ReviewStatus == TeacherFinancialReviewStatus.AutoApproved || x.ReviewStatus == TeacherFinancialReviewStatus.Approved) && x.TeacherFinancialEvent.OccurredAt >= input.PeriodFrom && x.TeacherFinancialEvent.OccurredAt <= input.PeriodTo);
        if (requested is { Count: > 0 }) query = query.Where(x => requested.Contains(x.Id));
        var allocations = await query.OrderBy(x => x.TeacherFinancialEvent.OccurredAt).ToListAsync(ct);
        if (requested is { Count: > 0 } && allocations.Count != requested.Count) return SettlementPreviewState.Invalid("بعض البنود غير مؤهلة أو تم حجزها في تسوية أخرى");
        var gross = allocations.Sum(x => x.TeacherShareAmount - x.ReversedAmount);
        var open = await db.TeacherPayoutAdjustments.Where(x => x.TeacherId == input.TeacherId && x.Status == TeacherPayoutAdjustmentStatus.Open && x.Amount < 0m
            && !db.TeacherSettlementLines.Any(line => line.AdjustmentId == x.Id
                && line.TeacherSettlement.Status != TeacherSettlementStatus.Cancelled
                && line.TeacherSettlement.Status != TeacherSettlementStatus.Paid)).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var selected = new List<SelectedDebt>(); var debt = 0m;
        foreach (var adjustment in open)
        {
            if (debt >= gross) break;
            var amount = Math.Min(-adjustment.Amount, gross - debt);
            debt += amount;
            selected.Add(new(adjustment, amount));
        }
        return new(null, allocations, selected, gross, debt);
    }

    private static TeacherSettlement NewSettlement(Guid actorId, SettlementCreationInput input, SettlementPreviewState preview) => new()
    { Id = Guid.NewGuid(), TeacherId = input.TeacherId, PeriodFrom = input.PeriodFrom, PeriodTo = input.PeriodTo, Currency = "EGP",
      Status = TeacherSettlementStatus.Draft, GrossDueAmount = preview.Gross, DebtDeductionAmount = preview.Debt,
      NetPayableAmount = preview.Gross - preview.Debt, Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(), CreatedByUserId = actorId };
    private static void ReserveAllocations(TeacherSettlement settlement, SettlementPreviewState preview)
    { foreach (var allocation in preview.Allocations) { var line = new TeacherSettlementLine { Id = Guid.NewGuid(), TeacherSettlementId = settlement.Id, AllocationId = allocation.Id, Amount = allocation.TeacherShareAmount - allocation.ReversedAmount, DescriptionSnapshot = allocation.ContentNameSnapshot }; settlement.Lines.Add(line); allocation.SettlementLineId = line.Id; allocation.PayoutStatus = TeacherFinancialPayoutStatus.Reserved; } foreach (var adjustment in preview.Adjustments) settlement.Lines.Add(new TeacherSettlementLine { Id = Guid.NewGuid(), TeacherSettlementId = settlement.Id, AdjustmentId = adjustment.Adjustment.Id, Amount = -adjustment.Deduction, DescriptionSnapshot = $"خصم مديونية: {adjustment.Adjustment.Reason}" }); }
    private static FinancialInvoice NewInvoice(Guid actorId, TeacherSettlement s) => new() { Id = Guid.NewGuid(), Type = FinancialInvoiceType.TeacherSettlement, Status = FinancialInvoiceStatus.Draft, DocumentNumber = $"TS-{DateTime.UtcNow:yyyyMMdd}-{s.Id.ToString("N")[..8].ToUpperInvariant()}", Currency = s.Currency, Amount = s.NetPayableAmount, TeacherId = s.TeacherId, TeacherSettlementId = s.Id, Description = $"تسوية مستحقات مدرس للفترة {s.PeriodFrom:yyyy-MM-dd} إلى {s.PeriodTo:yyyy-MM-dd}", CreatedByUserId = actorId };
    private static TeacherFinancialEvent NewReversal(TeacherReversalInput input, List<TeacherFinancialAllocation> allocations, Dictionary<Guid, decimal> amounts) { var id = Guid.NewGuid(); return new() { Id = id, SourceType = TeacherFinancialSourceType.Refund, SourceId = id, TargetType = allocations[0].TeacherFinancialEvent.TargetType, TargetId = allocations[0].TeacherFinancialEvent.TargetId, GrossAmount = -amounts.Values.Sum(), PlatformShareAmount = 0m, IdempotencyKey = input.IdempotencyKey.Trim(), DetailsJson = JsonSerializer.Serialize(new { input.Reason, input.Disposition, allocationIds = amounts.Keys }), OccurredAt = DateTime.UtcNow, ReviewStatus = TeacherFinancialReviewStatus.Reversed, PayoutStatus = TeacherFinancialPayoutStatus.Reversed }; }
    private void ApplyReversal(TeacherReversalInput input, List<TeacherFinancialAllocation> allocations, Dictionary<Guid, decimal> amounts, TeacherAccount? account, TeacherFinancialEvent reversal) { foreach (var allocation in allocations) { var amount = amounts[allocation.Id]; allocation.ReversedAmount += amount; var paid = allocation.PayoutStatus == TeacherFinancialPayoutStatus.Paid; if (!paid) { account!.CurrentBalance -= amount; account.TotalEarnings = Math.Max(0m, account.TotalEarnings - amount); account.UpdatedAt = DateTime.UtcNow; if (allocation.ReversedAmount == allocation.TeacherShareAmount) allocation.PayoutStatus = TeacherFinancialPayoutStatus.Reversed; } else { db.TeacherPayoutAdjustments.Add(new TeacherPayoutAdjustment { Id = Guid.NewGuid(), TeacherId = allocation.TeacherId, RelatedFinancialEventId = allocation.TeacherFinancialEventId, RelatedPayoutId = allocation.PayoutId, Amount = -amount, Reason = $"[{input.Disposition}] {input.Reason.Trim()}", Status = TeacherPayoutAdjustmentStatus.Open }); if (allocation.ReversedAmount == allocation.TeacherShareAmount) allocation.PayoutStatus = TeacherFinancialPayoutStatus.Debt; } reversal.Allocations.Add(new TeacherFinancialAllocation { Id = Guid.NewGuid(), TeacherId = allocation.TeacherId, AllocationMode = TeacherAllocationMode.Reversal, AllocationValue = amount, GrossBasisAmount = -amount, TeacherShareAmount = -amount, PlatformShareAmount = 0m, StudentNameSnapshot = allocation.StudentNameSnapshot, StudentPhoneSnapshot = allocation.StudentPhoneSnapshot, ContentNameSnapshot = allocation.ContentNameSnapshot, ReviewStatus = TeacherFinancialReviewStatus.Reversed, PayoutStatus = paid ? TeacherFinancialPayoutStatus.Debt : TeacherFinancialPayoutStatus.Reversed }); } }
    private static TeacherFinanceCommandResult Invalid(string message) => new(TeacherFinanceCommandStatus.Invalid, Message: message);
    private static TeacherFinanceCommandResult Conflict(string message) => new(TeacherFinanceCommandStatus.Conflict, Message: message);
    private static TeacherFinanceCommandResult NotFound(string message) => new(TeacherFinanceCommandStatus.NotFound, Message: message);
    private sealed record SelectedDebt(TeacherPayoutAdjustment Adjustment, decimal Deduction);
    private sealed record SettlementPreviewState(string? Error, List<TeacherFinancialAllocation> Allocations, List<SelectedDebt> Adjustments, decimal Gross, decimal Debt) { public static SettlementPreviewState Invalid(string error) => new(error, [], [], 0m, 0m); }
}
