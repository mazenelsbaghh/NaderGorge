using NaderGorge.Application.Common.Configuration;

namespace NaderGorge.Integration.Tests.Migrations;

public sealed class FinanceMigrationAuthorizationTests
{
    [Fact]
    public void Historical_migration_and_treasury_mutations_are_not_public_permissions()
    {
        Assert.Contains(PlatformFinancePermissions.HistoricalMigration, PlatformFinancePermissions.All);
        Assert.Contains(PlatformFinancePermissions.TreasuryManage, PlatformFinancePermissions.All);
        Assert.DoesNotContain("finance.migration.public", PlatformFinancePermissions.All);
    }
}
