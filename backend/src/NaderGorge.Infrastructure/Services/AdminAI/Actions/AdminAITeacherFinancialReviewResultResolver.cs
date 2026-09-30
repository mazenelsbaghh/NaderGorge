using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAITeacherFinancialReviewResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.finance.teacher-event.review";

    public async Task<AdminAIActionOutcome?> ResolveAsync(
        string externalOperationId, string executionId, CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal))
            return null;

        var exists = await db.TeacherFinancialAllocations.AsNoTracking()
            .AnyAsync(item => item.ReviewOperationId == externalOperationId, cancellationToken);
        return exists
            ? AdminAIActionOutcomeFactory.Success(new { Message = (string?)null },
                1, ["teacher-finance", "finance"])
            : null;
    }
}
