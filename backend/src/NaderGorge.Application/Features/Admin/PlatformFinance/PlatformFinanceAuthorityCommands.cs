using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance;

public sealed record BackfillWalletTransferReviewsCommand : IRequest<WalletTransferBackfillResult>;
public sealed record WalletTransferBackfillResult(int Added);

public sealed record RecordWalletTransferExpenseCommand(
    Guid ReviewId,
    Guid ActorUserId,
    string BeneficiaryName,
    string Reason,
    Guid CategoryId,
    Guid? CostCenterId) : IRequest<WalletTransferClassificationResult>;

public sealed record RecordWalletInternalTransferCommand(
    Guid ReviewId,
    Guid ActorUserId,
    Guid DestinationTreasuryAccountId) : IRequest<WalletTransferClassificationResult>;

public sealed record WalletTransferClassificationResult(
    Guid ReviewId,
    WalletTransferReviewStatus Status,
    Guid AuthorityRecordId,
    decimal? TotalDebited = null,
    bool AlreadyApplied = false);

public sealed record ReversePlatformExpenseCommand(Guid ExpenseId, Guid ActorUserId, string Reason)
    : IRequest<PlatformFinanceReversalResult>;

public sealed record ReversePlatformRefundCommand(Guid RefundId, Guid ActorUserId, string Reason)
    : IRequest<PlatformFinanceReversalResult>;

public sealed record PlatformFinanceReversalResult(
    Guid RecordId,
    Guid ReversalId,
    bool AlreadyApplied = false);

public sealed class BackfillWalletTransferReviewsCommandHandler(IAppDbContext db)
    : IRequestHandler<BackfillWalletTransferReviewsCommand, WalletTransferBackfillResult>
{
    public async Task<WalletTransferBackfillResult> Handle(
        BackfillWalletTransferReviewsCommand command,
        CancellationToken ct)
    {
        var reviewedIds = await db.WalletTransferReviews.AsNoTracking()
            .Select(review => review.IncomingSmsLogId).ToArrayAsync(ct);
        var logs = await db.IncomingSmsLogs.AsNoTracking()
            .Where(log => !reviewedIds.Contains(log.Id))
            .OrderBy(log => log.ReceivedAt).ToArrayAsync(ct);
        var reviews = logs.Select(CreateReview).Where(review => review is not null).Cast<WalletTransferReview>().ToArray();
        if (reviews.Length == 0) return new(0);
        db.WalletTransferReviews.AddRange(reviews);
        await db.SaveChangesAsync(ct);
        return new(reviews.Length);
    }

    private static WalletTransferReview? CreateReview(IncomingSmsLog log)
    {
        var parsed = SmsParser.Parse(log.Body);
        if (!SmsParser.IsOutgoingTransfer(log.Body) || !parsed.Amount.HasValue) return null;
        return new WalletTransferReview
        {
            IncomingSmsLogId = log.Id,
            SourceWalletId = log.WalletId,
            DestinationPhoneNumber = parsed.RecipientPhone ?? parsed.SenderPhone ?? "غير معروف",
            Amount = parsed.Amount.Value,
            ServiceFee = parsed.ServiceFee,
            TransferReference = parsed.TransferReference,
            OccurredAt = log.ReceivedAt
        };
    }
}

public sealed class RecordWalletTransferExpenseCommandHandler(
    IAppDbContext db,
    IPlatformFinanceOperationsService operations)
    : IRequestHandler<RecordWalletTransferExpenseCommand, WalletTransferClassificationResult>
{
    public async Task<WalletTransferClassificationResult> Handle(
        RecordWalletTransferExpenseCommand command,
        CancellationToken ct)
    {
        Validate(command);
        var review = await db.WalletTransferReviews.SingleOrDefaultAsync(x => x.Id == command.ReviewId, ct)
            ?? throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_REVIEW_NOT_FOUND");
        if (review.Status == WalletTransferReviewStatus.RecordedAsExpense && review.PlatformExpenseId.HasValue)
            return Result(review, review.PlatformExpenseId.Value, true);
        EnsurePending(review);
        var treasuryId = await SourceTreasuryIdAsync(db, review.SourceWalletId, ct);
        var vendor = await VendorAsync(db, command.BeneficiaryName.Trim(), review.DestinationPhoneNumber, ct);
        var totalDebited = review.Amount + review.ServiceFee;
        var description = Description(review, command.BeneficiaryName.Trim(), command.Reason.Trim());
        var expense = await operations.CreateExpenseAsync(new(totalDebited, review.OccurredAt, command.CategoryId,
            command.CostCenterId, vendor.Id, description, $"WLT-{review.Id:N}", command.ActorUserId), ct);
        await operations.PostExpenseAsync(expense.Id,
            new(treasuryId, command.ActorUserId, $"wallet-expense-{review.Id:N}", description), ct);
        ClassifyAsExpense(review, expense.Id, command.ActorUserId);
        await db.SaveChangesAsync(ct);
        return Result(review, expense.Id, false);
    }

    private static void Validate(RecordWalletTransferExpenseCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.BeneficiaryName) || string.IsNullOrWhiteSpace(command.Reason))
            throw new InvalidOperationException("FINANCE_BENEFICIARY_AND_REASON_REQUIRED");
    }

    private static string Description(WalletTransferReview review, string beneficiary, string reason)
    {
        var description = $"تحويل محفظة إلى {beneficiary} ({review.DestinationPhoneNumber}) — {reason}";
        return review.ServiceFee > 0m
            ? $"{description} (يشمل رسوم تحويل {review.ServiceFee:0.##} ج.م)"
            : description;
    }

    private static void ClassifyAsExpense(WalletTransferReview review, Guid expenseId, Guid actorUserId)
    {
        review.PlatformExpenseId = expenseId;
        review.Status = WalletTransferReviewStatus.RecordedAsExpense;
        review.ClassifiedByUserId = actorUserId;
        review.ClassifiedAt = DateTime.UtcNow;
    }

    private static WalletTransferClassificationResult Result(
        WalletTransferReview review,
        Guid expenseId,
        bool alreadyApplied) => new(review.Id, review.Status, expenseId, review.Amount + review.ServiceFee, alreadyApplied);

    private static async Task<FinanceVendor> VendorAsync(
        IAppDbContext db,
        string name,
        string phone,
        CancellationToken ct)
    {
        var existing = await db.FinanceVendors.SingleOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null) return existing;
        var vendor = new FinanceVendor { Name = name, Phone = phone };
        db.FinanceVendors.Add(vendor);
        await db.SaveChangesAsync(ct);
        return vendor;
    }

    internal static void EnsurePending(WalletTransferReview review)
    {
        if (review.Status != WalletTransferReviewStatus.PendingClassification)
            throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_ALREADY_CLASSIFIED");
    }

    internal static async Task<Guid> SourceTreasuryIdAsync(
        IAppDbContext db,
        Guid walletId,
        CancellationToken ct) => await db.TreasuryAccounts
        .Where(x => x.DigitalWalletId == walletId && x.IsActive)
        .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("FINANCE_WALLET_TREASURY_NOT_FOUND");
}

public sealed class RecordWalletInternalTransferCommandHandler(
    IAppDbContext db,
    IPlatformFinancePlanningService planning)
    : IRequestHandler<RecordWalletInternalTransferCommand, WalletTransferClassificationResult>
{
    public async Task<WalletTransferClassificationResult> Handle(
        RecordWalletInternalTransferCommand command,
        CancellationToken ct)
    {
        var review = await db.WalletTransferReviews.SingleOrDefaultAsync(x => x.Id == command.ReviewId, ct)
            ?? throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_REVIEW_NOT_FOUND");
        if (review.Status == WalletTransferReviewStatus.RecordedAsInternalTransfer && review.TreasuryTransferId.HasValue)
            return new(review.Id, review.Status, review.TreasuryTransferId.Value, AlreadyApplied: true);
        RecordWalletTransferExpenseCommandHandler.EnsurePending(review);
        if (review.ServiceFee > 0m) throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_FEE_REQUIRES_EXPENSE");
        var sourceId = await RecordWalletTransferExpenseCommandHandler.SourceTreasuryIdAsync(db, review.SourceWalletId, ct);
        var transfer = await planning.TransferAsync(new(sourceId, command.DestinationTreasuryAccountId, review.Amount,
            $"تحويل داخلي من رسالة محفظة {review.TransferReference ?? review.Id.ToString("N")}",
            command.ActorUserId, $"wallet-internal-{review.Id:N}"), ct);
        review.TreasuryTransferId = transfer.Id;
        review.Status = WalletTransferReviewStatus.RecordedAsInternalTransfer;
        review.ClassifiedByUserId = command.ActorUserId;
        review.ClassifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(review.Id, review.Status, transfer.Id);
    }
}

public sealed class ReversePlatformExpenseCommandHandler(IAppDbContext db, IFinancialPostingService posting)
    : IRequestHandler<ReversePlatformExpenseCommand, PlatformFinanceReversalResult>
{
    public async Task<PlatformFinanceReversalResult> Handle(ReversePlatformExpenseCommand command, CancellationToken ct)
    {
        var expense = await db.PlatformExpenses.SingleOrDefaultAsync(x => x.Id == command.ExpenseId, ct)
            ?? throw new InvalidOperationException("FINANCE_EXPENSE_NOT_FOUND");
        var journalId = expense.JournalEntryId ?? throw new InvalidOperationException("FINANCE_EXPENSE_NOT_POSTED");
        if (expense.Status == PlatformExpenseStatus.Reversed)
            return new(expense.Id, await ReversalIdAsync(db, journalId, ct), true);
        var reversal = await posting.ReverseAsync(journalId, command.ActorUserId, command.Reason, ct);
        expense.Status = PlatformExpenseStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return new(expense.Id, reversal.Id);
    }

    internal static async Task<Guid> ReversalIdAsync(IAppDbContext db, Guid journalId, CancellationToken ct)
        => await db.JournalEntries.Where(x => x.IdempotencyKey == $"reversal:{journalId:N}")
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("FINANCE_REVERSAL_JOURNAL_NOT_FOUND");
}

public sealed class ReversePlatformRefundCommandHandler(IAppDbContext db, IFinancialPostingService posting)
    : IRequestHandler<ReversePlatformRefundCommand, PlatformFinanceReversalResult>
{
    public async Task<PlatformFinanceReversalResult> Handle(ReversePlatformRefundCommand command, CancellationToken ct)
    {
        var refund = await db.PlatformRefunds.SingleOrDefaultAsync(x => x.Id == command.RefundId, ct)
            ?? throw new InvalidOperationException("FINANCE_REFUND_NOT_FOUND");
        var journalId = refund.JournalEntryId ?? throw new InvalidOperationException("FINANCE_REFUND_NOT_POSTED");
        if (refund.Status == PlatformRefundStatus.Reversed)
            return new(refund.Id, await ReversePlatformExpenseCommandHandler.ReversalIdAsync(db, journalId, ct), true);
        var reversal = await posting.ReverseAsync(journalId, command.ActorUserId, command.Reason, ct);
        refund.Status = PlatformRefundStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return new(refund.Id, reversal.Id);
    }
}
