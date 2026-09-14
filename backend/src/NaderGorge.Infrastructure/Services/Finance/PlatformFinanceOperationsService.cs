using Microsoft.EntityFrameworkCore;
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
    private readonly BalanceService _balanceService = balanceService;
    private readonly RefundPostingService? _refundPosting = refundPosting;

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

        var sourceAmount = await _db.SalesFinancialEffects
            .Where(x => x.PurchaseOperationId == request.OriginalSourceId)
            .Select(x => (decimal?)x.PaidAmount)
            .SingleOrDefaultAsync(ct);
        if (sourceAmount is null)
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

        if (_refundPosting is not null)
            return await _refundPosting.PostAsync(refund, idempotencyKey, actorUserId, ct);

        var creditAccount = refund.Method == PlatformRefundMethod.Cash
            ? await GetTreasuryAccountCodeAsync(refund.TreasuryAccountId!.Value, ct)
            : "1100";
        var lines = new List<FinancialPostingLine>
        {
            new("4100", refund.PlatformAmount, 0m, StudentId: refund.StudentId),
            new(creditAccount, 0m, refund.TotalAmount, StudentId: refund.StudentId, TreasuryAccountId: refund.TreasuryAccountId)
        };
        if (refund.TeacherAmount > 0m)
        {
            lines[0] = new FinancialPostingLine("4100", refund.PlatformAmount, 0m, StudentId: refund.StudentId);
            lines.Insert(1, new FinancialPostingLine("2000", refund.TeacherAmount, 0m, StudentId: refund.StudentId, TeacherId: refund.TeacherId));
        }

        var transaction = _db is DbContext context
            && context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            && context.Database.CurrentTransaction is null
            ? await _db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct)
            : null;
        try
        {
            var journal = await _posting.PostAsync(new FinancialPostingRequest(
                "PlatformRefund", refund.Id, "RefundPost", idempotencyKey,
                refund.Reason, DateTime.UtcNow, actorUserId, lines), ct);
            if (refund.Method == PlatformRefundMethod.StudentBalance)
            {
                await _balanceService.AddCredit(
                    refund.StudentId,
                    refund.TotalAmount,
                    $"استرداد مالي: {refund.Reason}",
                    refund.Id,
                    "PlatformRefund",
                    ct);
            }
            refund.JournalEntryId = journal.Id;
            refund.Status = PlatformRefundStatus.Posted;
            await _db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
            return refund;
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<string> GetTreasuryAccountCodeAsync(Guid treasuryAccountId, CancellationToken ct)
    {
        var code = await (from treasury in _db.TreasuryAccounts
                          join account in _db.FinancialAccounts on treasury.FinancialAccountId equals account.Id
                          where treasury.Id == treasuryAccountId && treasury.IsActive && account.IsActive
                          select account.Code).SingleOrDefaultAsync(ct);
        return code ?? throw new InvalidOperationException("FINANCE_TREASURY_NOT_FOUND");
    }
}
