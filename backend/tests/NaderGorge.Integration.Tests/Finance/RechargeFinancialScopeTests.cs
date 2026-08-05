using NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class RechargeFinancialScopeTests
{
    [Fact]
    public void Refund_and_recharge_scope_validation_rejects_ambiguous_mutations()
    {
        Assert.Throws<InvalidOperationException>(() => PlatformRefundCommandValidator.ValidateDraft(new(Guid.NewGuid(), "Purchase", Guid.NewGuid(), null, 1m, 0m, PlatformRefundMethod.Cash, null, "cash", null)));
        PlatformRefundCommandValidator.ValidateDraft(new(Guid.NewGuid(), "Purchase", Guid.NewGuid(), null, 1m, 0m, PlatformRefundMethod.StudentBalance, null, "balance", null));
    }
}
