using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

public sealed class PlatformFinanceOperationsService(
    IAppDbContext db,
    IFinancialPostingService posting,
    BalanceService balanceService,
    RefundPostingService? refundPosting = null) : IPlatformFinanceOperationsService
{
    private readonly IAppDbContext _db = db;
    private readonly IFinancialPostingService _posting = posting;
    private readonly RefundPostingService _refundPosting = refundPosting ?? new RefundPostingService(db, posting, balanceService);

    public async Task<PlatformExpense> CreateExpenseAsync(CreatePlatformExpenseRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0m) throw new ArgumentOutOfRangeException(nameof(request.Amount));
        if (!await _db.ExpenseCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive, ct))
            throw new InvalidOperationException("FINANCE_EXPENSE_CATEGORY_NOT_FOUND");

        var expense = new PlatformExpense
        {
            DocumentNumber = string.IsNullOrWhiteSpace(request.DocumentNumber) ? $"EXP-{DateTime.UtcNow:yyyyMMddHHmmssfff}" : request.DocumentNumber.Trim(),
            Amount = decimal.Round(request.Amount, 2),
            OccurredAt = request.OccurredAt,
            CategoryId = request.CategoryId,
            CostCenterId = request.CostCenterId,
            VendorId = request.VendorId,
            Description = request.Description.Trim(),
            CreatedByUserId = request.CreatedByUserId
        };
        _db.PlatformExpenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        return expense;
    }

    public async Task<PlatformExpense> PostExpenseAsync(Guid expenseId, PostPlatformExpenseRequest request, CancellationToken ct)
    {
        var expense = await _db.PlatformExpenses.SingleOrDefaultAsync(x => x.Id == expenseId, ct)
            ?? throw new InvalidOperationException("FINANCE_EXPENSE_NOT_FOUND");
        if (expense.Status != PlatformExpenseStatus.Draft)
            throw new InvalidOperationException("FINANCE_ALREADY_POSTED");

        var category = await _db.ExpenseCategories.SingleAsync(x => x.Id == expense.CategoryId, ct);
        var paid = request.TreasuryAccountId.HasValue;
        var treasuryCode = paid ? await GetTreasuryAccountCodeAsync(request.TreasuryAccountId!.Value, ct) : "1000";
        var journal = await _posting.PostAsync(new FinancialPostingRequest(
            "PlatformExpense", expense.Id, "ExpensePost", request.IdempotencyKey,
            expense.Description, expense.OccurredAt, request.ActorUserId,
            paid
                ? [new FinancialPostingLine(category.AccountCode, expense.Amount, 0m, TreasuryAccountId: request.TreasuryAccountId), new FinancialPostingLine(treasuryCode, 0m, expense.Amount, TreasuryAccountId: request.TreasuryAccountId)]
                : [new FinancialPostingLine(category.AccountCode, expense.Amount, 0m), new FinancialPostingLine("2100", 0m, expense.Amount)]), ct);

        expense.JournalEntryId = journal.Id;
        expense.TreasuryAccountId = request.TreasuryAccountId;
        expense.Status = paid ? PlatformExpenseStatus.Paid : PlatformExpenseStatus.PostedUnpaid;
        await _db.SaveChangesAsync(ct);
        return expense;
    }

    public async Task<ExpensePayment> PayExpenseAsync(Guid expenseId, PayPlatformExpenseRequest request, CancellationToken ct)
    {
        var expense = await _db.PlatformExpenses.SingleOrDefaultAsync(x => x.Id == expenseId, ct)
            ?? throw new InvalidOperationException("FINANCE_EXPENSE_NOT_FOUND");
        if (expense.Status is PlatformExpenseStatus.Draft or PlatformExpenseStatus.Paid or PlatformExpenseStatus.Reversed)
            throw new InvalidOperationException("FINANCE_EXPENSE_NOT_PAYABLE");
        var paid = await _db.ExpensePayments.Where(x => x.PlatformExpenseId == expenseId).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        if (request.Amount <= 0m || paid + request.Amount > expense.Amount)
            throw new InvalidOperationException("FINANCE_AMOUNT_EXCEEDED");
        var treasuryCode = await GetTreasuryAccountCodeAsync(request.TreasuryAccountId, ct);

        var journal = await _posting.PostAsync(new FinancialPostingRequest(
            "PlatformExpense", expense.Id, "ExpensePayment", request.IdempotencyKey,
            $"Payment for {expense.DocumentNumber}", request.Amount == expense.Amount ? DateTime.UtcNow : expense.OccurredAt,
            request.ActorUserId,
            [new FinancialPostingLine("2100", request.Amount, 0m, TreasuryAccountId: request.TreasuryAccountId), new FinancialPostingLine(treasuryCode, 0m, request.Amount, TreasuryAccountId: request.TreasuryAccountId)]), ct);

        var payment = new ExpensePayment
        {
            PlatformExpenseId = expense.Id,
            Amount = request.Amount,
            TreasuryAccountId = request.TreasuryAccountId,
            PaymentReference = request.PaymentReference.Trim(),
            JournalEntryId = journal.Id,
            PaidByUserId = request.ActorUserId
        };
        _db.ExpensePayments.Add(payment);
        expense.Status = paid + request.Amount >= expense.Amount ? PlatformExpenseStatus.Paid : PlatformExpenseStatus.PartiallyPaid;
        await _db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task<PlatformRefund> CreateRefundAsync(CreatePlatformRefundRequest request, CancellationToken ct)
    {
        if (request.PlatformAmount < 0m || request.TeacherAmount < 0m || request.PlatformAmount + request.TeacherAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(request.PlatformAmount));
        var method = (PlatformRefundMethod)request.Method;
        if (!Enum.IsDefined(method)) throw new InvalidOperationException("FINANCE_INVALID_REFUND_METHOD");
        if (method == PlatformRefundMethod.Cash && !request.TreasuryAccountId.HasValue)
            throw new InvalidOperationException("FINANCE_TREASURY_REQUIRED");

        var sourceAmount = await ResolveRefundSourceAmountAsync(request, ct);
        if (sourceAmount is null || sourceAmount <= 0m)
            throw new InvalidOperationException("FINANCE_REFUND_SOURCE_NOT_FOUND");

        var alreadyRefunded = await _db.PlatformRefunds
            .Where(x => x.OriginalSourceId == request.OriginalSourceId && x.Status != PlatformRefundStatus.Reversed)
            .SumAsync(x => (decimal?)(x.PlatformAmount + x.TeacherAmount), ct) ?? 0m;
        if (alreadyRefunded + request.PlatformAmount + request.TeacherAmount > sourceAmount.Value)
            throw new InvalidOperationException("FINANCE_REFUND_AMOUNT_EXCEEDED");

        var refund = new PlatformRefund
        {
            OriginalSourceId = request.OriginalSourceId,
            OriginalSourceType = request.OriginalSourceType.Trim(),
            StudentId = request.StudentId,
            TeacherId = request.TeacherId,
            PlatformAmount = decimal.Round(request.PlatformAmount, 2),
            TeacherAmount = decimal.Round(request.TeacherAmount, 2),
            Method = method,
            TreasuryAccountId = request.TreasuryAccountId,
            Reason = request.Reason.Trim(),
            PaymentReference = request.PaymentReference?.Trim(),
            CreatedByUserId = request.CreatedByUserId
        };
        _db.PlatformRefunds.Add(refund);
        await _db.SaveChangesAsync(ct);
        return refund;
    }

    public async Task<PlatformRefund> PostRefundAsync(Guid refundId, string idempotencyKey, Guid actorUserId, CancellationToken ct)
    {
        var refund = await _db.PlatformRefunds.SingleOrDefaultAsync(x => x.Id == refundId, ct)
            ?? throw new InvalidOperationException("FINANCE_REFUND_NOT_FOUND");
        if (refund.Status != PlatformRefundStatus.Draft)
            throw new InvalidOperationException("FINANCE_ALREADY_POSTED");

        return await _refundPosting.PostAsync(refund, idempotencyKey, actorUserId, ct);
    }

    private async Task<string> GetTreasuryAccountCodeAsync(Guid treasuryAccountId, CancellationToken ct)
    {
        var code = await (from treasury in _db.TreasuryAccounts
                          join account in _db.FinancialAccounts on treasury.FinancialAccountId equals account.Id
                          where treasury.Id == treasuryAccountId && treasury.IsActive && account.IsActive
                          select account.Code).SingleOrDefaultAsync(ct);
        return code ?? throw new InvalidOperationException("FINANCE_TREASURY_NOT_FOUND");
    }

    private async Task<decimal?> ResolveRefundSourceAmountAsync(CreatePlatformRefundRequest request, CancellationToken ct)
    {
        var sourceAmount = await _db.SalesFinancialEffects
            .Where(x => x.PurchaseOperationId == request.OriginalSourceId)
            .Select(x => (decimal?)(x.PaidAmount > 0m ? x.PaidAmount : x.GrossAmount))
            .SingleOrDefaultAsync(ct);
        if (sourceAmount > 0m || !request.AccessGrantId.HasValue) return sourceAmount;
        if (request.OriginalSourceType == "HistoricalAccessGrant" && request.AccessGrantId != request.OriginalSourceId)
            return sourceAmount;

        var grant = await _db.StudentAccessGrants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.AccessGrantId.Value && x.UserId == request.StudentId, ct);
        if (grant is null) return null;
        return await RefundGrantPriceResolver.ResolveManualCeilingAsync(_db, grant, ct);
    }
}
