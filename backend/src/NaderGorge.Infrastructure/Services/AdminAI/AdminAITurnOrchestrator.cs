using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAITurnOrchestrator(IAppDbContext db, IAdminAIAccessGate access, IAdminAIDataProtector protector) : IAdminAITurnOrchestrator
{
    public async Task<AdminAITurnDto> QueueAsync(Guid actorId, Guid conversationId, string content, long expectedVersion, string idempotencyKey, CancellationToken ct)
    {
        await using var transaction = await BeginSerializableIfSupportedAsync(ct);
        var accessState = await access.RequireCurrentAdminAsync(actorId, null, ct);
        var normalizedContent = content.Trim();
        if (normalizedContent.Length is < 1 or > 8000 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("Invalid turn request.");
        var digest = protector.Digest("turn-callback", Encoding.UTF8.GetBytes($"{actorId:N}:{idempotencyKey}"));
        var admissionPayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{conversationId:N}:{expectedVersion}:{normalizedContent}"))).ToLowerInvariant();
        var replay = await db.AdminAITurns.AsNoTracking().SingleOrDefaultAsync(x => x.CallbackIdempotencyDigest == digest, ct);
        if (replay is not null)
        {
            if (replay.AdmissionPayloadHash != admissionPayloadHash) throw new InvalidOperationException("Idempotency payload conflict.");
            return ToDto(replay);
        }
        var conversation = await db.AdminAIConversations.SingleOrDefaultAsync(x => x.Id == conversationId && x.OwnerAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (conversation.Status != AdminAIConversationStatus.Active || conversation.Version != expectedVersion) throw new InvalidOperationException("Conversation is unavailable or stale.");
        if (await db.AdminAITurns.AnyAsync(x => x.ConversationId == conversationId && x.Status != AdminAITurnStatus.Completed && x.Status != AdminAITurnStatus.Cancelled && x.Status != AdminAITurnStatus.Failed && x.Status != AdminAITurnStatus.AccessRevoked, ct)) throw new InvalidOperationException("Conversation already has an active turn.");
        if (await db.AdminAITurns.CountAsync(x => x.ActorAdminUserId == actorId && x.Status != AdminAITurnStatus.Completed && x.Status != AdminAITurnStatus.Cancelled && x.Status != AdminAITurnStatus.Failed && x.Status != AdminAITurnStatus.AccessRevoked, ct) >= 2) throw new InvalidOperationException("Admin active turn limit reached.");
        var baseline = await db.AdminAICapabilityBaselines.AsNoTracking().SingleOrDefaultAsync(x => x.Status == AdminAICapabilityBaselineStatus.Active, ct) ?? throw new InvalidOperationException("Capability baseline is unavailable.");
        var policy = await db.AdminAISensitiveDataPolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Status == AdminAISensitiveDataPolicyStatus.Active, ct) ?? throw new InvalidOperationException("Sensitive policy is unavailable.");
        conversation.LastSequence++; conversation.Version++; conversation.LastActivityAt = DateTime.UtcNow;
        var message = new AdminAIMessage { ConversationId = conversationId, Sequence = conversation.LastSequence, Role = AdminAIMessageRole.Admin, Content = normalizedContent };
        var turn = new AdminAITurn { ConversationId = conversationId, SourceMessageId = message.Id, ActorAdminUserId = actorId, CapabilityBaselineId = baseline.Id, SensitiveDataPolicyVersionId = policy.Id, ExpectedConversationVersion = conversation.Version, ExpectedSecurityVersion = accessState.SecurityVersion, CallbackIdempotencyDigest = digest, AdmissionPayloadHash = admissionPayloadHash };
        var step = new AdminAITurnStep { TurnId = turn.Id, StepNumber = 1, Status = AdminAITurnStepStatus.Queued, ExpectedTurnVersion = turn.Version };
        db.AdminAIMessages.Add(message); db.AdminAITurns.Add(turn); db.AdminAITurnSteps.Add(step);
        db.OutboxEvents.Add(new OutboxEvent { Type = "AdminAITurnQueued", PayloadJson = JsonSerializer.Serialize(new { schemaVersion = "1", turnId = turn.Id, conversationId, queuedAt = turn.QueuedAt }) });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ToDto(turn);
    }

    public async Task<AdminAITurnDto> CancelAsync(Guid actorId, Guid conversationId, Guid turnId, long expectedVersion, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        var turn = await db.AdminAITurns.SingleOrDefaultAsync(x => x.Id == turnId && x.ConversationId == conversationId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (turn.Version != expectedVersion) throw new InvalidOperationException("Turn version conflict.");
        if (!turn.Status.IsTerminal()) { turn.Status = AdminAITurnStatus.CancelRequested; turn.CancellationRequestedAt = DateTime.UtcNow; turn.Version++; await db.SaveChangesAsync(ct); }
        return ToDto(turn);
    }

    private static AdminAITurnDto ToDto(AdminAITurn x) => new(x.Id, x.Status, x.CurrentStepNumber, x.ReadInvocationCount, x.FailureCode, x.QueuedAt, x.CompletedAt, x.Version);

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginSerializableIfSupportedAsync(CancellationToken ct)
    {
        if (db is not DbContext context || !context.Database.IsRelational() || context.Database.CurrentTransaction is not null) return null;
        return await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    }
}
