using NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.Finance;

public sealed class RefundTeacherAccountingTests
{
    [Fact]
    public void Teacher_amount_is_required_to_be_explicit_and_non_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformRefundCommandValidator.ValidateDraft(new(Guid.NewGuid(), "Purchase", Guid.NewGuid(), Guid.NewGuid(), -1m, 0m, PlatformRefundMethod.StudentBalance, null, "reason", null)));
        PlatformRefundCommandValidator.ValidateDraft(new(Guid.NewGuid(), "Purchase", Guid.NewGuid(), Guid.NewGuid(), 1m, 2m, PlatformRefundMethod.StudentBalance, null, "reason", null));
    }
}
