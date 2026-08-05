using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.Finance.Adapters;

public sealed class SalesFinancialAdapter(IFinancialPostingService posting) : IFinancialSourceAdapter
{
    public bool CanHandle(string sourceType) => sourceType is "Purchase" or "DirectSale" or "CodeSale" or "PublicExamSale" or "SharedPackageSale";

    public Task<JournalEntry> PostAsync(FinanceSourcePostingRequest request, CancellationToken ct)
    {
        var lines = new List<FinancialPostingLine> { new("1100", request.Amount, 0m, StudentId: request.StudentId) };
        if (request.PlatformAmount > 0m) lines.Add(new("4000", 0m, request.PlatformAmount, StudentId: request.StudentId));
        if (request.TeacherAmount > 0m) lines.Add(new("2000", 0m, request.TeacherAmount, StudentId: request.StudentId, TeacherId: request.TeacherId));
        return posting.PostAsync(new FinancialPostingRequest(request.SourceType, request.SourceId, "Sale", request.IdempotencyKey, "بيع محتوى", request.OccurredAt, request.ActorUserId, lines), ct);
    }
}
