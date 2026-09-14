using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Services;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIRecoveryService(IAppDbContext db, ILiveSupportAIHandoffService handoff) : ILiveSupportAIRecoveryService
{
    public async Task<LiveSupportAIRecoveryBatchResultDto> RecoverBatchAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken)
    {
        batchSize = Math.Clamp(batchSize, 1, 500);
        var staleTurns = await db.LiveSupportAITurns
            .Where(item => (item.Status == LiveSupportAITurnStatus.Queued || item.Status == LiveSupportAITurnStatus.Processing || item.Status == LiveSupportAITurnStatus.ProviderCompleted) &&
                           db.LiveSupportConversations.Any(conversation => conversation.Id == item.ConversationId &&
                               conversation.AllowsAI &&
                               conversation.Status != LiveSupportConversationStatus.Closed &&
                               conversation.Status != LiveSupportConversationStatus.Abandoned) &&
                           ((item.Status == LiveSupportAITurnStatus.Queued && item.QueuedAt < utcNow.AddMinutes(-5)) ||
                            (item.Status != LiveSupportAITurnStatus.Queued && item.StartedAt < utcNow.AddMinutes(-10))))
            .OrderBy(item => item.QueuedAt).Take(batchSize).ToListAsync(cancellationToken);
        var forcedHandoffs = new HashSet<Guid>();
        foreach (var turn in staleTurns)
        {
            turn.Status = LiveSupportAITurnStatus.Failed;
            turn.FailureCode = "AI_TURN_STALE";
            turn.CallbackStatus = LiveSupportAICallbackStatus.Failed;
            turn.LastSafeCallbackErrorCode = "RECOVERY_TIMEOUT";
            turn.CompletedAt = utcNow;
            turn.Version++;
            forcedHandoffs.Add(turn.ConversationId);
        }
        var expiredDecisions = await db.LiveSupportAIPendingActions
            .Where(item => item.Status == LiveSupportAIPendingActionStatus.PendingConfirmation && item.ExpiresAt <= utcNow)
            .OrderBy(item => item.ExpiresAt).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var decision in expiredDecisions)
        {
            decision.Status = LiveSupportAIPendingActionStatus.Expired;
            decision.CompletedAt = utcNow;
            decision.Version++;
        }
        var expiredVerifications = await db.LiveSupportAIVerificationSessions
            .Where(item => (item.Status == LiveSupportAIVerificationStatus.AwaitingLookup || item.Status == LiveSupportAIVerificationStatus.Challenging) &&
                           item.ExpiresAt <= utcNow &&
                           db.LiveSupportConversations.Any(conversation =>
                               conversation.Id == item.ConversationId && conversation.AllowsAI))
            .OrderBy(item => item.ExpiresAt).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var verification in expiredVerifications)
        {
            verification.Status = LiveSupportAIVerificationStatus.Failed;
            verification.LockedAt = utcNow;
            verification.CompletedAt = utcNow;
            verification.Version++;
            forcedHandoffs.Add(verification.ConversationId);
        }
        var disabledStates = await db.LiveSupportAIConversationStates
            .Where(item => item.Mode == LiveSupportAIMode.AiActive && item.DisableRequestedAt != null &&
                           db.LiveSupportConversations.Any(conversation => conversation.Id == item.ConversationId &&
                               conversation.AllowsAI &&
                               conversation.Status != LiveSupportConversationStatus.Closed &&
                               conversation.Status != LiveSupportConversationStatus.Abandoned))
            .OrderBy(item => item.DisableRequestedAt).Take(batchSize).Select(item => item.ConversationId).ToListAsync(cancellationToken);
        forcedHandoffs.UnionWith(disabledStates);
        var inactiveStates = await db.LiveSupportAIConversationStates
            .Where(item => item.Mode == LiveSupportAIMode.AiActive && item.LastParticipantActivityAt < utcNow.AddMinutes(-30) &&
                           db.LiveSupportConversations.Any(conversation => conversation.Id == item.ConversationId &&
                               conversation.AllowsAI &&
                               conversation.Status != LiveSupportConversationStatus.Closed &&
                               conversation.Status != LiveSupportConversationStatus.Abandoned))
            .OrderBy(item => item.LastParticipantActivityAt).Take(batchSize).ToListAsync(cancellationToken);
        var inactiveConversationIds = inactiveStates.Select(item => item.ConversationId).ToList();
        var inactiveConversations = await db.LiveSupportConversations
            .Where(item => inactiveConversationIds.Contains(item.Id) && item.AllowsAI && item.Status != LiveSupportConversationStatus.Closed && item.Status != LiveSupportConversationStatus.Abandoned)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var activeAssignments = await db.LiveSupportAssignments
            .Where(item => inactiveConversationIds.Contains(item.ConversationId) && item.EndedAt == null)
            .ToListAsync(cancellationToken);
        var queuedEntries = await db.LiveSupportQueueEntries
            .Where(item => inactiveConversationIds.Contains(item.ConversationId) && item.DequeuedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var state in inactiveStates)
        {
            if (!inactiveConversations.TryGetValue(state.ConversationId, out var conversation)) continue;
            conversation.Status = LiveSupportConversationStatus.Closed;
            conversation.ClosedAt = utcNow;
            conversation.ClosedByUserId = null;
            conversation.CloseReason = "AUTO_CLOSED_INACTIVE";
            conversation.CurrentOwnerUserId = null;
            conversation.Version++;
            state.AutoCloseAt = utcNow;
            state.ResolvedAt = utcNow;
            state.ResolutionCode = "INACTIVITY_TIMEOUT";
            state.LastRecoveryAt = utcNow;
            state.Version++;
            var eventId = Guid.NewGuid();
            db.LiveSupportEvents.Add(new LiveSupportEvent
            {
                Id = eventId,
                ConversationId = conversation.Id,
                Type = LiveSupportEventType.AIAutoClosed,
                OccurredAt = utcNow,
                Sequence = utcNow.Ticks,
                SafeMetadataJson = JsonSerializer.Serialize(new { reasonCode = "INACTIVITY_TIMEOUT" })
            });
            db.OutboxEvents.Add(new NaderGorge.Domain.Entities.OutboxEvent
            {
                Type = "LiveSupportEvent",
                TargetGroup = $"LiveSupport:Conversation:{conversation.Id:N}",
                PayloadJson = JsonSerializer.Serialize(new { eventId, conversationId = conversation.Id, sequence = utcNow.Ticks, occurredAt = utcNow, type = "AIAutoClosed", payload = new { reasonCode = "INACTIVITY_TIMEOUT" } })
            });
        }
        foreach (var assignment in activeAssignments) { assignment.EndedAt = utcNow; assignment.EndReason = LiveSupportAssignmentEndReason.Closed; }
        foreach (var entry in queuedEntries) { entry.DequeuedAt = utcNow; entry.DequeueReason = "Closed"; }
        forcedHandoffs.ExceptWith(inactiveConversationIds);
        await db.SaveChangesAsync(cancellationToken);

        var reconciled = 0;
        foreach (var conversationId in forcedHandoffs.Take(batchSize))
        {
            await handoff.HandoffAsync(conversationId, null, null, "AI_RECOVERY", "تم تحويل المحادثة للدعم البشري بعد تعذر استكمال المساعد.", true, $"recovery:{conversationId:N}", cancellationToken);
            reconciled++;
        }
        LiveSupportAITelemetry.RecoveryOutcomes.Add(reconciled, new KeyValuePair<string, object?>("outcome", "reconciled"));
        return new LiveSupportAIRecoveryBatchResultDto(staleTurns.Count, expiredDecisions.Count, expiredVerifications.Count, inactiveConversations.Count, reconciled);
    }
}
