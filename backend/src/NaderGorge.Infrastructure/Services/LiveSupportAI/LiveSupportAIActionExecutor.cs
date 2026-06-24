using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIActionExecutor(
    ILiveSupportActionService actions,
    IConfiguration configuration,
    IAppDbContext db) : ILiveSupportAIActionExecutor
{
    public async Task<Guid> ExecuteAsync(
        Guid conversationId,
        Guid studentUserId,
        Guid pendingDecisionId,
        string actionKey,
        IReadOnlyDictionary<string, object?> payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var systemActorId = await ResolveSystemActorAsync(cancellationToken);
        var catalog = await actions.GetCatalogAsync(systemActorId, true, conversationId, cancellationToken);
        var definition = catalog.SingleOrDefault(item => item.Key == actionKey)
            ?? throw new LiveSupportException("ACTION_NOT_ALLOWED", "الإجراء غير متاح.");
        var result = await actions.ExecuteAsync(new LiveSupportActionRequest(
            systemActorId,
            true,
            conversationId,
            actionKey,
            idempotencyKey,
            definition.ConfirmationVersion,
            JsonSerializer.SerializeToElement(payload)), cancellationToken);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "AILiveSupportConfirmedAction",
            EntityType = "LiveSupportAIPendingAction",
            EntityId = pendingDecisionId,
            PerformedByUserId = systemActorId,
            NewValues = JsonSerializer.Serialize(new { conversationId, studentUserId, actionKey, executionId = result.ExecutionId }),
            IpAddress = "AI_SYSTEM"
        });
        await db.SaveChangesAsync(cancellationToken);
        return result.ExecutionId;
    }

    private async Task<Guid> ResolveSystemActorAsync(CancellationToken cancellationToken)
    {
        if (Guid.TryParse(configuration["LiveSupportAI:SystemActorUserId"], out var configured) &&
            await db.Users.AnyAsync(item => item.Id == configured && item.IsActive, cancellationToken)) return configured;
        return await db.UserRoles.Where(item => item.Role.Type == RoleType.Admin && item.User.IsActive)
            .Select(item => item.UserId).OrderBy(item => item).FirstOrDefaultAsync(cancellationToken) is var id && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("AI_SYSTEM_ACTOR_NOT_CONFIGURED");
    }
}
