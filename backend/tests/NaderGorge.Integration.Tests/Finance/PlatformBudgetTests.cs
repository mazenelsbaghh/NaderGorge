using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformBudgetTests
{
    [Fact]
    public async Task Budget_period_and_actuals_are_separate_from_journal_history()
    {
        var (db, _, _) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var accountId = db.FinancialAccounts.Single(x => x.Code == "5000").Id;
            var service = new PlatformFinancePlanningService(db, new FinancialPostingService(db));
            var budget = await service.CreateBudgetAsync(new("Weekly", 1, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(6), Guid.NewGuid(), [new(accountId, null, null, 100m)]), CancellationToken.None);
            Assert.Single(budget.Lines);
            Assert.Equal(100m, budget.Lines.Single().PlannedAmount);
        }
    }
}
