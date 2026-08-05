using NaderGorge.Application.Common.Configuration;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformFinanceAuthorizationTests
{
    [Fact]
    public void Finance_permissions_are_granular_and_include_staff_read_actions()
    {
        Assert.Contains(PlatformFinancePermissions.DashboardView, PlatformFinancePermissions.All);
        Assert.Contains(PlatformFinancePermissions.ExpensesView, PlatformFinancePermissions.All);
        Assert.Contains(PlatformFinancePermissions.RefundsView, PlatformFinancePermissions.All);
        Assert.DoesNotContain("finance.admin", PlatformFinancePermissions.All);
    }
}
