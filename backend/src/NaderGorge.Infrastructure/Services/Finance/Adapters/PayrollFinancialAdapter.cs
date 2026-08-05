using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.Finance.Adapters;

public sealed class PayrollFinancialAdapter(IFinancialPostingService posting) : IFinancialSourceAdapter
{
    public bool CanHandle(string sourceType) => sourceType.Equals("Payroll", StringComparison.OrdinalIgnoreCase);

    public Task<JournalEntry> PostAsync(FinanceSourcePostingRequest request, CancellationToken ct) => posting.PostAsync(new FinancialPostingRequest(
        "Payroll", request.SourceId, "PayrollPayment", request.IdempotencyKey, "سداد رواتب", request.OccurredAt, request.ActorUserId,
        [new("5100", request.Amount, 0m), new("1000", 0m, request.Amount)]), ct);
}
