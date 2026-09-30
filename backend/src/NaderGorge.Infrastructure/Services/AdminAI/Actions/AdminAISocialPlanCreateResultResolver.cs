using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAISocialPlanCreateResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.tools.social-plan.create";

    public async Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal)) return null;
        var receipt = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == externalOperationId
                && item.Scope == "social-plan.create", cancellationToken);
        return receipt is null
            ? null
            : AdminAIActionOutcomeFactory.Success(new { socialPlanId = receipt.ResultEntityId },
                1, ["social-plans"]);
    }
}
