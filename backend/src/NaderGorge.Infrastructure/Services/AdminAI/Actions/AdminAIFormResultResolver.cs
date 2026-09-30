using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAIFormResultResolver(
    IAppDbContext db, string capabilityKey, string receiptScope, bool isUpdate)
    : IAdminAIExternalResultResolver
{
    public string CapabilityKey => capabilityKey;

    public async Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal)) return null;
        var receipt = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == externalOperationId
                && item.Scope == receiptScope, cancellationToken);
        if (receipt is null || (isUpdate && receipt.SafeResultJson != "{\"updated\":true}")) return null;
        return isUpdate
            ? AdminAIActionOutcomeFactory.Success(new { updated = true }, 1, ["forms"])
            : AdminAIActionOutcomeFactory.Success(new { formId = receipt.ResultEntityId }, 1, ["forms"]);
    }
}
