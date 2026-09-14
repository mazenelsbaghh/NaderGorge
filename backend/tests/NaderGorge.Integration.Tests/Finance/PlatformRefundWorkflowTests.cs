using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Services;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformRefundWorkflowTests
{
    [Fact]
    public async Task Refund_cannot_exceed_authoritative_sale_amount()
    {
        var (db, treasuryId, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var sourceId = Guid.NewGuid();
            db.SalesFinancialEffects.Add(new() { PurchaseOperationId = sourceId, StudentId = studentId, PaidAmount = 50m, GrossAmount = 50m, PlatformShareImpact = 50m });
            await db.SaveChangesAsync();
            var service = new PlatformFinanceOperationsService(db, new FinancialPostingService(db), new BalanceService(db, NullLogger<BalanceService>.Instance));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRefundAsync(new(sourceId, "Purchase", studentId, null, 51m, 0m, 1, null, "too much", null, Guid.NewGuid()), CancellationToken.None));
            _ = treasuryId;
        }
    }
}
