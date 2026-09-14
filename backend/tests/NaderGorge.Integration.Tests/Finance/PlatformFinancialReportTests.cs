using NaderGorge.Application.Features.Admin.PlatformFinance.Reports;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformFinancialReportTests
{
    [Fact]
    public async Task Report_dataset_balances_and_can_be_filtered_to_profit_loss()
    {
        var (db, _, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            await new FinancialPostingService(db).PostAsync(new("Purchase", Guid.NewGuid(), "Sale", "report-1", "report", DateTime.UtcNow, null, [new("1000", 10m, 0m, StudentId: studentId), new("4000", 0m, 10m, StudentId: studentId)]));
            var report = await new PlatformFinancialReportQueries(db).GetAsync("profit-loss", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), CancellationToken.None);
            Assert.Equal(report.TotalDebit, report.TotalCredit);
            Assert.Contains(report.Rows, row => row.Code == "4000");
        }
    }
}
