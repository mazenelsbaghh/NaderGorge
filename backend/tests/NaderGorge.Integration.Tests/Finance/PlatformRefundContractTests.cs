using NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformRefundContractTests
{
    [Fact]
    public void Refund_contract_requires_evidence_and_method_specific_treasury()
    {
        Assert.Throws<InvalidOperationException>(() => PlatformRefundCommandValidator.ValidateDraft(new(Guid.NewGuid(), "Purchase", Guid.NewGuid(), null, 10m, 0m, PlatformRefundMethod.Cash, null, "", null)));
        PlatformRefundCommandValidator.ValidateDraft(new(Guid.NewGuid(), "Purchase", Guid.NewGuid(), null, 10m, 0m, PlatformRefundMethod.StudentBalance, null, "student request", null));
    }
}
