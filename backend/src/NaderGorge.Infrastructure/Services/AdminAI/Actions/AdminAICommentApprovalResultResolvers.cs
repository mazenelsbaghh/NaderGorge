using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAICommunityCommentApprovalResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.assessment.community-comment.approve";

    public async Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal)) return null;
        var receipt = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == externalOperationId
                && item.Scope == "community-comment.approve", cancellationToken);
        if (receipt?.SafeResultJson is null) return null;
        var result = JsonSerializer.Deserialize<ModerateCommunityCommentResponse>(receipt.SafeResultJson);
        if (result?.CommentId != receipt.ResultEntityId)
            throw new InvalidOperationException("Community comment approval receipt result does not match its target.");
        return AdminAIActionOutcomeFactory.Success(result, 1, ["community-comments", "moderation"]);
    }
}

public sealed class AdminAILessonCommentApprovalResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.assessment.lesson-comment.approve";

    public async Task<AdminAIActionOutcome?> ResolveAsync(string externalOperationId, string executionId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal)) return null;
        var receipt = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == externalOperationId
                && item.Scope == "lesson-comment.approve", cancellationToken);
        if (receipt?.SafeResultJson is null) return null;
        var result = JsonSerializer.Deserialize<ModerateLessonCommentResponse>(receipt.SafeResultJson);
        if (result?.Id != receipt.ResultEntityId)
            throw new InvalidOperationException("Lesson comment approval receipt result does not match its target.");
        return AdminAIActionOutcomeFactory.Success(result, 1, ["lesson-comments", "moderation"]);
    }
}
