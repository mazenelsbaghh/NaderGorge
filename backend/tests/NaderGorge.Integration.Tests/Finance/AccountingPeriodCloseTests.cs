using NaderGorge.Application.Features.Admin.PlatformFinance.Periods;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class AccountingPeriodCloseTests
{
    [Fact]
    public async Task Close_and_reopen_require_reason_and_leave_audit_evidence()
    {
        await using var db = FinanceTestDbFactory.Create();
        var period = new AccountingPeriod { StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(1) };
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        var commands = new AccountingPeriodCommands(db);
        await commands.CloseAsync(period.Id, Guid.NewGuid(), "month-end", CancellationToken.None);
        await commands.ReopenAsync(period.Id, Guid.NewGuid(), "correction", CancellationToken.None);
        Assert.Equal(2, db.AuditLogs.Count());
    }
}
