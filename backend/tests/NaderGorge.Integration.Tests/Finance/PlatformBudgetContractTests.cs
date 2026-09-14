using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformBudgetContractTests
{
    [Fact]
    public async Task Budget_rejects_invalid_period_and_negative_planned_amount()
    {
        var (db, _, _) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var service = new PlatformFinancePlanningService(db, new FinancialPostingService(db));
            var accountId = db.FinancialAccounts.First().Id;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBudgetAsync(new("bad", 99, DateTime.UtcNow.Date, DateTime.UtcNow.Date, Guid.NewGuid(), [new(accountId, null, null, 1m)]), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBudgetAsync(new("bad", 1, DateTime.UtcNow.Date.AddDays(1), DateTime.UtcNow.Date, Guid.NewGuid(), [new(accountId, null, null, 1m)]), CancellationToken.None));
        }
    }
}
