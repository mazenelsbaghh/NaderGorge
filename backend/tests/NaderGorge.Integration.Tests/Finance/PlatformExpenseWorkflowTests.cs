using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformExpenseWorkflowTests
{
    [Fact]
    public async Task Paid_expense_and_partial_payment_never_overpay()
    {
        var (db, treasuryId, _) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var category = new ExpenseCategory { Name = $"test-{Guid.NewGuid():N}", AccountCode = "5000" };
            db.ExpenseCategories.Add(category);
            await db.SaveChangesAsync();
            var service = new PlatformFinanceOperationsService(db, new FinancialPostingService(db), new BalanceService(db, NullLogger<BalanceService>.Instance));
            var expense = await service.CreateExpenseAsync(new(100m, DateTime.UtcNow, category.Id, null, null, "test expense", null, Guid.NewGuid()), CancellationToken.None);
            await service.PostExpenseAsync(expense.Id, new(treasuryId, Guid.NewGuid(), "expense-test", null), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayExpenseAsync(expense.Id, new(treasuryId, 1m, "over", Guid.NewGuid(), "expense-over"), CancellationToken.None));
        }
    }
}
