using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAIWatchRequestApprovalResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.identity.watch-request.approve";

    public async Task<AdminAIActionOutcome?> ResolveAsync(
        string externalOperationId, string executionId, CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal))
            return null;

        var exists = await db.VideoOverrides.AsNoTracking()
            .AnyAsync(item => item.OperationId == externalOperationId, cancellationToken);
        return exists
            ? AdminAIActionOutcomeFactory.Success(new { Message = (string?)null },
                1, ["watch-progress", "watch-requests"])
            : null;
    }
}
