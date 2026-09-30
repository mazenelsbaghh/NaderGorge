using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAITaskOperationResultResolver(
    IAppDbContext db, string capabilityKey, string receiptScope, IReadOnlyList<string> refreshScopes)
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
        if (receipt?.SafeResultJson is null) return null;
        return AdminAIActionOutcomeFactory.Success(
            JsonSerializer.Deserialize<JsonElement>(receipt.SafeResultJson), 1, refreshScopes);
    }
}
