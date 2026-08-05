using NaderGorge.Application.Features.Admin.PlatformFinance.Expenses;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformExpenseContractTests
{
    [Fact]
    public void Expense_contract_rejects_missing_description_and_idempotency()
    {
        Assert.Throws<InvalidOperationException>(() => PlatformExpenseCommandValidator.ValidateDraft(new(1m, DateTime.UtcNow, Guid.NewGuid(), null, null, "", null)));
        Assert.Throws<InvalidOperationException>(() => PlatformExpenseCommandValidator.ValidatePosting(new(Guid.NewGuid(), null, "", null)));
        PlatformExpenseCommandValidator.ValidatePayment(new(Guid.NewGuid(), Guid.NewGuid(), 1m, "cash-1", "pay-1"));
    }
}
