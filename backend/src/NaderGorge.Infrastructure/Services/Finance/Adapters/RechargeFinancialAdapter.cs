using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.Finance.Adapters;

public sealed class RechargeFinancialAdapter(IFinancialPostingService posting) : IFinancialSourceAdapter
{
    public bool CanHandle(string sourceType) => sourceType.Equals("RechargeRequest", StringComparison.OrdinalIgnoreCase);

    public Task<JournalEntry> PostAsync(FinanceSourcePostingRequest request, CancellationToken ct) => posting.PostAsync(new FinancialPostingRequest(
        "RechargeRequest", request.SourceId, "Recharge", request.IdempotencyKey,
        request.TeacherId.HasValue ? "شحن رصيد مدرس" : "شحن رصيد عام", request.OccurredAt, request.ActorUserId,
        [new("1000", request.Amount, 0m, StudentId: request.StudentId), new(request.TeacherId.HasValue ? "1110" : "1100", 0m, request.Amount, StudentId: request.StudentId, TeacherId: request.TeacherId)]), ct);
}
