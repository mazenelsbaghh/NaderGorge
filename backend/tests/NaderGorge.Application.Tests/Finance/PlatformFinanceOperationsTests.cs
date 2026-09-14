using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace NaderGorge.Application.Tests.Finance;

public sealed class PlatformFinanceOperationsTests
{
    [Fact]
    public async Task Paid_expense_posts_to_operating_expense_and_treasury()
    {
        await using var db = TestAppDbContextFactory.Create();
        var account = new FinancialAccount { Code = "5000", Name = "Expense", Type = FinancialAccountType.Expense, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.OperatingExpense };
        var cash = new FinancialAccount { Code = "1000", Name = "Cash", Type = FinancialAccountType.Asset, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury };
        var category = new ExpenseCategory { Name = "Operations", AccountCode = "5000" };
        var treasury = new TreasuryAccount { Name = "Cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = cash.Id };
        db.AddRange(account, cash, category, treasury);
        await db.SaveChangesAsync();
        var operations = new PlatformFinanceOperationsService(db, new FinancialPostingService(db), new BalanceService(db, NullLogger<BalanceService>.Instance));

        var expense = await operations.CreateExpenseAsync(new CreatePlatformExpenseRequest(250m, DateTime.UtcNow, category.Id, null, null, "Internet", null, Guid.NewGuid()), CancellationToken.None);
        var posted = await operations.PostExpenseAsync(expense.Id, new PostPlatformExpenseRequest(treasury.Id, Guid.NewGuid(), "expense-post-1", null), CancellationToken.None);

        Assert.Equal(PlatformExpenseStatus.Paid, posted.Status);
        var journal = Assert.Single(db.JournalEntries);
        Assert.Equal(250m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(250m, journal.Lines.Sum(line => line.Credit));
    }

    [Fact]
    public async Task Cash_refund_requires_treasury_and_posts_platform_and_teacher_lines()
    {
        await using var db = TestAppDbContextFactory.Create();
        db.FinancialAccounts.AddRange(
            new FinancialAccount { Code = "1000", Name = "Cash", Type = FinancialAccountType.Asset, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury },
            new FinancialAccount { Code = "2000", Name = "Teacher payable", Type = FinancialAccountType.Liability, NormalSide = FinancialNormalSide.Credit, Role = FinancialAccountRole.TeacherPayable },
            new FinancialAccount { Code = "4100", Name = "Refunds", Type = FinancialAccountType.ContraRevenue, NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Refunds });
        var cash = db.FinancialAccounts.Local.Single(x => x.Code == "1000");
        var treasury = new TreasuryAccount { Name = "Cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = cash.Id };
        db.TreasuryAccounts.Add(treasury);
        var purchaseId = Guid.NewGuid();
        db.SalesFinancialEffects.Add(new SalesFinancialEffect
        {
            PurchaseOperationId = purchaseId,
            StudentId = Guid.NewGuid(),
            TargetType = SalesTargetType.Package,
            TargetId = Guid.NewGuid(),
            PaidAmount = 100m,
            GrossAmount = 100m,
            PlatformShareImpact = 80m,
            TeacherShareImpact = 20m
        });
        await db.SaveChangesAsync();
        var operations = new PlatformFinanceOperationsService(db, new FinancialPostingService(db), new BalanceService(db, NullLogger<BalanceService>.Instance));
        var refund = await operations.CreateRefundAsync(new CreatePlatformRefundRequest(purchaseId, "Purchase", Guid.NewGuid(), Guid.NewGuid(), 80m, 20m, 2, treasury.Id, "Student request", "REF-1", Guid.NewGuid()), CancellationToken.None);

        await operations.PostRefundAsync(refund.Id, "refund-post-1", Guid.NewGuid(), CancellationToken.None);

        var journal = Assert.Single(db.JournalEntries);
        Assert.Equal(100m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, journal.Lines.Sum(line => line.Credit));
        Assert.Contains(journal.Lines, line => line.FinancialAccountId == cash.Id && line.Credit == 100m);
    }
}
