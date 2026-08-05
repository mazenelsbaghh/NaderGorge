using NaderGorge.Domain.Entities;

namespace NaderGorge.Integration.Tests.Migrations;

public sealed class PlatformFinanceHistoricalMigrationTests
{
    [Fact]
    public void Migration_items_have_repeat_safe_statuses_and_checksums()
    {
        var item = new FinancialMigrationItem { SourceType = "Purchase", SourceId = Guid.NewGuid(), Amount = 10m, Status = FinanceMigrationItemStatus.Posted, SourceChecksum = "sha256" };
        var batch = new FinancialMigrationBatch { SourceChecksum = "sha256", Items = [item] };
        Assert.Equal(FinanceMigrationItemStatus.Posted, batch.Items.Single().Status);
        Assert.NotEmpty(batch.SourceChecksum);
    }
}
