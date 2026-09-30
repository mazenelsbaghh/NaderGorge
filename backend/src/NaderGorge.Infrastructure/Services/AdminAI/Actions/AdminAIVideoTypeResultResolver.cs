using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAIVideoTypeResultResolver(
    IAppDbContext db, string capabilityKey, string receiptScope) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => capabilityKey;

    public async Task<AdminAIActionOutcome?> ResolveAsync(
        string externalOperationId, string executionId, CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal))
            return null;

        var receipt = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == externalOperationId
                && item.Scope == receiptScope, cancellationToken);
        if (receipt?.SafeResultJson is null)
            return null;
        var safeResult = JsonSerializer.Deserialize<VideoTypeDto>(receipt.SafeResultJson);
        if (safeResult?.Id != receipt.ResultEntityId)
            throw new InvalidOperationException("Video type receipt result does not match its entity identity.");
        return AdminAIActionOutcomeFactory.Success(safeResult, 1, ["video-types", "content"]);
    }
}
