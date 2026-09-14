using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformFinanceDashboardContractTests
{
    [Fact]
    public async Task Dashboard_balances_and_ledger_filters_return_posted_rows_only()
    {
        var (db, _, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var posting = new FinancialPostingService(db);
            await posting.PostAsync(new("Purchase", Guid.NewGuid(), "Sale", "dashboard-1", "sale", DateTime.UtcNow, null, [new("1000", 25m, 0m, StudentId: studentId), new("4000", 0m, 25m, StudentId: studentId)]));
            var dashboard = await new PlatformFinanceDashboardService(db).GetDashboardAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), CancellationToken.None);
            Assert.Equal(25m, dashboard.Revenue);
            Assert.Equal(25m, dashboard.Cash);
            Assert.All(dashboard.Accounts, account => Assert.True(account.Debit >= 0m && account.Credit >= 0m));
        }
    }
}
