using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAICommunityPostApprovalResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.assessment.community-post.approve";

    public async Task<AdminAIActionOutcome?> ResolveAsync(
        string externalOperationId, string executionId, CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal))
            return null;
        var receipt = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == externalOperationId
                && item.Scope == "community-post.approve", cancellationToken);
        if (receipt?.SafeResultJson is null)
            return null;
        var result = JsonSerializer.Deserialize<ModerateCommunityPostResponse>(receipt.SafeResultJson);
        if (result?.Id != receipt.ResultEntityId)
            throw new InvalidOperationException("Community post approval receipt result does not match its target.");
        return AdminAIActionOutcomeFactory.Success(result, 1, ["community-posts", "moderation"]);
    }
}
