using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

public sealed class PlatformFinanceAuthorityCommandTests
{
    [Fact]
    public async Task Wallet_review_backfill_is_replay_safe()
    {
        await using var db = TestAppDbContextFactory.Create();
        var wallet = new DigitalWallet { Label = "Operations", PhoneNumber = "01000000000" };
        var log = new IncomingSmsLog
        {
            WalletId = wallet.Id,
            Sender = "VodafoneCash",
            Body = "تم تحويل مبلغ 150 جنيه إلى رقم 01011111111 رسوم التحويل 2 جنيه رقم العملية 77",
            ReceivedAt = DateTime.UtcNow,
            DeduplicationHash = "wallet-review-backfill"
        };
        db.AddRange(wallet, log);
        await db.SaveChangesAsync();
        var handler = new BackfillWalletTransferReviewsCommandHandler(db);

        var first = await handler.Handle(new(), CancellationToken.None);
        var replay = await handler.Handle(new(), CancellationToken.None);

        Assert.Equal(1, first.Added);
        Assert.Equal(0, replay.Added);
        var review = Assert.Single(db.WalletTransferReviews);
        Assert.Equal(log.Id, review.IncomingSmsLogId);
        Assert.Equal(152m, review.Amount + review.ServiceFee);
    }

    [Fact]
    public async Task Wallet_expense_replay_returns_the_original_expense()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedWalletExpenseAsync(db);
        var posting = new FinancialPostingService(db);
        var operations = new PlatformFinanceOperationsService(
            db,
            posting,
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var handler = new RecordWalletTransferExpenseCommandHandler(db, operations);
        var command = new RecordWalletTransferExpenseCommand(
            fixture.Review.Id, Guid.NewGuid(), "Internet Provider", "Monthly bill", fixture.Category.Id, null);

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        Assert.False(first.AlreadyApplied);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal(first.AuthorityRecordId, replay.AuthorityRecordId);
        Assert.Single(db.PlatformExpenses);
        Assert.Single(db.FinanceVendors);
    }

    [Fact]
    public async Task Expense_reversal_replay_returns_the_original_reversal()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedWalletExpenseAsync(db);
        var posting = new FinancialPostingService(db);
        var operations = new PlatformFinanceOperationsService(
            db,
            posting,
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var expense = await operations.CreateExpenseAsync(new(50m, DateTime.UtcNow, fixture.Category.Id,
            null, null, "Connectivity", "EXP-REVERSE", Guid.NewGuid()), CancellationToken.None);
        await operations.PostExpenseAsync(expense.Id,
            new(fixture.Treasury.Id, Guid.NewGuid(), "expense-reversal-source", null), CancellationToken.None);
        var handler = new ReversePlatformExpenseCommandHandler(db, posting);
        var command = new ReversePlatformExpenseCommand(expense.Id, Guid.NewGuid(), "Duplicate expense");

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        Assert.False(first.AlreadyApplied);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal(first.ReversalId, replay.ReversalId);
        Assert.Equal(2, db.JournalEntries.Count());
    }

    [Fact]
    public async Task Refund_reversal_replay_returns_the_original_reversal()
    {
        await using var db = TestAppDbContextFactory.Create();
        var cashAccount = new FinancialAccount
        {
            Code = "1000", Name = "Cash", Type = FinancialAccountType.Asset,
            NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury
        };
        var refundAccount = new FinancialAccount
        {
            Code = "4100", Name = "Refund", Type = FinancialAccountType.ContraRevenue,
            NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Refunds
        };
        var treasury = new TreasuryAccount
        {
            Name = "Cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = cashAccount.Id
        };
        var purchaseId = Guid.NewGuid();
        db.AddRange(cashAccount, refundAccount, treasury, new SalesFinancialEffect
        {
            PurchaseOperationId = purchaseId, StudentId = Guid.NewGuid(), TargetType = SalesTargetType.Package,
            TargetId = Guid.NewGuid(), PaidAmount = 75m, GrossAmount = 75m, PlatformShareImpact = 75m
        });
        await db.SaveChangesAsync();
        var posting = new FinancialPostingService(db);
        var operations = new PlatformFinanceOperationsService(
            db,
            posting,
            new BalanceService(db, NullLogger<BalanceService>.Instance));
        var refund = await operations.CreateRefundAsync(new(purchaseId, "PurchaseOperation", Guid.NewGuid(),
            null, 75m, 0m, (int)PlatformRefundMethod.Cash, treasury.Id, "Customer refund", null,
            Guid.NewGuid()), CancellationToken.None);
        await operations.PostRefundAsync(refund.Id, "refund-reversal-source", Guid.NewGuid(), CancellationToken.None);
        var handler = new ReversePlatformRefundCommandHandler(db, posting);
        var command = new ReversePlatformRefundCommand(refund.Id, Guid.NewGuid(), "Refund correction");

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        Assert.False(first.AlreadyApplied);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal(first.ReversalId, replay.ReversalId);
        Assert.Equal(2, db.JournalEntries.Count());
    }

    private static async Task<WalletExpenseFixture> SeedWalletExpenseAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var expenseAccount = new FinancialAccount
        {
            Code = "5000", Name = "Expense", Type = FinancialAccountType.Expense,
            NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.OperatingExpense
        };
        var treasuryAccount = new FinancialAccount
        {
            Code = "1000", Name = "Wallet", Type = FinancialAccountType.Asset,
            NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury
        };
        var wallet = new DigitalWallet { Label = "Operations", PhoneNumber = "01000000000" };
        var treasury = new TreasuryAccount
        {
            Name = "Operations wallet", Type = TreasuryAccountType.DigitalWallet,
            FinancialAccountId = treasuryAccount.Id, DigitalWalletId = wallet.Id
        };
        var category = new ExpenseCategory { Name = "Connectivity", AccountCode = expenseAccount.Code };
        var review = new WalletTransferReview
        {
            IncomingSmsLogId = Guid.NewGuid(), SourceWalletId = wallet.Id,
            DestinationPhoneNumber = "01011111111", Amount = 100m, ServiceFee = 2m,
            OccurredAt = DateTime.UtcNow
        };
        db.AddRange(expenseAccount, treasuryAccount, wallet, treasury, category, review);
        await db.SaveChangesAsync();
        return new(review, category, treasury);
    }

    private sealed record WalletExpenseFixture(
        WalletTransferReview Review,
        ExpenseCategory Category,
        TreasuryAccount Treasury);
}
