using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Application.Features.LiveSupportAI.Services;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIHandoffService(IAppDbContext db, ILiveSupportAssignmentCoordinator assignmentCoordinator) : ILiveSupportAIHandoffService
{
    public async Task<string> HandoffAsync(Guid conversationId, LiveSupportParticipantIdentity? participant, Guid? actorUserId, string reasonCode, string safeSummary, bool forced, string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (db is AppDbContext relational && relational.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            await relational.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended({0}, 146))", [conversationId.ToString("N")], cancellationToken);
        var conversation = await db.LiveSupportConversations.SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        if (participant is not null &&
            ((participant.StudentUserId.HasValue && participant.StudentUserId != conversation.StudentUserId) ||
             (participant.GuestSessionId.HasValue && participant.GuestSessionId != conversation.GuestSessionId)))
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك الوصول لهذه المحادثة.");
        var state = await db.LiveSupportAIConversationStates.SingleOrDefaultAsync(item => item.ConversationId == conversationId, cancellationToken)
            ?? throw new LiveSupportException("AI_STATE_NOT_FOUND", "حالة المساعد غير متاحة.");
        if (state.Mode is LiveSupportAIMode.HumanQueued or LiveSupportAIMode.HumanAssigned)
        {
            await transaction.CommitAsync(cancellationToken);
            return "REPLAYED";
        }
        if (!forced)
        {
            var proposal = await db.LiveSupportAIPendingActions.SingleOrDefaultAsync(item => item.ConversationId == conversationId && item.DecisionKind == LiveSupportAIPendingDecisionKind.Handoff && item.Status == LiveSupportAIPendingActionStatus.PendingConfirmation, cancellationToken)
                ?? throw new LiveSupportException("HANDOFF_CONFIRMATION_REQUIRED", "يجب تأكيد التحويل أولًا.");
            proposal.Status = LiveSupportAIPendingActionStatus.Succeeded;
            proposal.ConfirmedAt = DateTime.UtcNow;
            proposal.CompletedAt = DateTime.UtcNow;
            proposal.ConfirmedByUserId = participant?.StudentUserId;
            proposal.ConfirmedByGuestSessionId = participant?.GuestSessionId;
            proposal.Version++;
        }
        var now = DateTime.UtcNow;
        foreach (var pending in await db.LiveSupportAIPendingActions.Where(item => item.ConversationId == conversationId && item.Status == LiveSupportAIPendingActionStatus.PendingConfirmation).ToListAsync(cancellationToken))
        {
            pending.Status = LiveSupportAIPendingActionStatus.Invalidated;
            pending.FailureCode = "HUMAN_HANDOFF";
            pending.CompletedAt = now;
            pending.Version++;
        }
        foreach (var turn in await db.LiveSupportAITurns.Where(item => item.ConversationId == conversationId && (item.Status == LiveSupportAITurnStatus.Queued || item.Status == LiveSupportAITurnStatus.Processing || item.Status == LiveSupportAITurnStatus.ProviderCompleted)).ToListAsync(cancellationToken))
        {
            turn.Status = LiveSupportAITurnStatus.DiscardedAfterHandoff;
            turn.CallbackStatus = LiveSupportAICallbackStatus.Discarded;
            turn.LastSafeCallbackErrorCode = "HUMAN_HANDOFF";
            turn.CompletedAt = now;
            turn.Version++;
        }
        state.Mode = LiveSupportAIMode.HumanQueued;
        state.HandoffReasonCode = reasonCode[..Math.Min(reasonCode.Length, 100)];
        state.HandoffSafeSummary = safeSummary[..Math.Min(safeSummary.Length, 2_000)];
        state.HandedOffAt = now;
        state.Version++;
        if (!await db.LiveSupportQueueEntries.AnyAsync(item => item.ConversationId == conversationId && item.DequeuedAt == null, cancellationToken))
            db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversationId, EnteredAt = now, Sequence = now.Ticks });
        conversation.Status = LiveSupportConversationStatus.Waiting;
        conversation.CurrentOwnerUserId = null;
        conversation.QueuedAt ??= now;
        conversation.Version++;
        var eventId = Guid.NewGuid();
        db.LiveSupportEvents.Add(new LiveSupportEvent { Id = eventId, ConversationId = conversationId, Type = LiveSupportEventType.AIHandoffCompleted, ActorUserId = actorUserId, ActorGuestSessionId = participant?.GuestSessionId, OccurredAt = now, Sequence = now.Ticks, SafeMetadataJson = JsonSerializer.Serialize(new { reasonCode, forced }) });
        db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Conversation:{conversationId:N}", PayloadJson = JsonSerializer.Serialize(new { eventId, conversationId, sequence = now.Ticks, occurredAt = now, type = "AIHandoffCompleted", payload = new { reasonCode, forced } }) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await assignmentCoordinator.AssignWaitingAsync(cancellationToken);
        LiveSupportAITelemetry.Handoffs.Add(1, new KeyValuePair<string, object?>("reason_code", reasonCode), new KeyValuePair<string, object?>("forced", forced));
        return "HANDED_OFF";
    }
}
