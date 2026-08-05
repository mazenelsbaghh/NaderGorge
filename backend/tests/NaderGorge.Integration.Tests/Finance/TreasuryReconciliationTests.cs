using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class TreasuryReconciliationTests
{
    [Fact]
    public async Task Treasury_transfer_rejects_same_source_and_destination()
    {
        var (db, treasuryId, _) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var service = new PlatformFinancePlanningService(db, new FinancialPostingService(db));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferAsync(new(treasuryId, treasuryId, 1m, "invalid", Guid.NewGuid(), "transfer-invalid"), CancellationToken.None));
        }
    }
}
