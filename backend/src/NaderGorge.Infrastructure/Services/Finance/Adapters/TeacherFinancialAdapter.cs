using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.Finance.Adapters;

public sealed class TeacherFinancialAdapter(IFinancialPostingService posting) : IFinancialSourceAdapter
{
    public bool CanHandle(string sourceType) => sourceType is "TeacherSettlement" or "TeacherPayout";

    public Task<JournalEntry> PostAsync(FinanceSourcePostingRequest request, CancellationToken ct) => posting.PostAsync(new FinancialPostingRequest(
        request.SourceType, request.SourceId, "TeacherSettlement", request.IdempotencyKey, "تسوية مستحقات مدرس", request.OccurredAt, request.ActorUserId,
        [new("2000", request.Amount, 0m, TeacherId: request.TeacherId), new("1000", 0m, request.Amount, TeacherId: request.TeacherId)]), ct);
}
