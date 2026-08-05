using System.Diagnostics;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformFinancePerformanceTests
{
    [Fact]
    public async Task Dashboard_query_is_bounded_for_a_representative_journal()
    {
        var (db, _, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var posting = new FinancialPostingService(db);
            for (var i = 0; i < 25; i++) await posting.PostAsync(new("Purchase", Guid.NewGuid(), "Sale", $"perf-{i}", "perf", DateTime.UtcNow, null, [new("1000", 1m, 0m, StudentId: studentId), new("4000", 0m, 1m, StudentId: studentId)]));
            var stopwatch = Stopwatch.StartNew();
            var result = await new PlatformFinanceDashboardService(db).GetDashboardAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), CancellationToken.None);
            stopwatch.Stop();
            Assert.NotEmpty(result.Accounts);
            Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 5000);
        }
    }
}
