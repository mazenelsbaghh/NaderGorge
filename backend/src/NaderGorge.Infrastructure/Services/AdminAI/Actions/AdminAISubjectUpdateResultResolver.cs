using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAISubjectUpdateResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.content.subject.update";

    public async Task<AdminAIActionOutcome?> ResolveAsync(
        string externalOperationId, string executionId, CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal))
            return null;

        var exists = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .AnyAsync(item => item.OperationId == externalOperationId
                && item.Scope == "subject.update", cancellationToken);
        return exists
            ? AdminAIActionOutcomeFactory.Success(new { updated = true }, 1, ["subjects", "content"])
            : null;
    }
}
