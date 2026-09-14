using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class FinancialLedgerFoundationTests
{
    [Fact]
    public async Task Idempotent_retry_and_closed_period_are_enforced()
    {
        var (db, _, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var service = new FinancialPostingService(db);
            var request = new FinancialPostingRequest("Recharge", Guid.NewGuid(), "Test", "foundation-1", "test", DateTime.UtcNow, null, [new("1000", 10m, 0m), new("1100", 0m, 10m, StudentId: studentId)]);
            var first = await service.PostAsync(request);
            var retry = await service.PostAsync(request);
            Assert.Equal(first.Id, retry.Id);
            db.AccountingPeriods.Add(new() { StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date, Status = AccountingPeriodStatus.Closed });
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(request with { IdempotencyKey = "foundation-closed" }));
        }
    }
}
