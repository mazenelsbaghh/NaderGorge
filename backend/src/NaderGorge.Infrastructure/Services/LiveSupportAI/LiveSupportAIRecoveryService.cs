using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
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
            .Where(item => (item.Status == LiveSupportAIVerificationStatus.AwaitingLookup || item.Status == LiveSupportAIVerificationStatus.Challenging) && item.ExpiresAt <= utcNow)
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
            .Where(item => item.Mode == LiveSupportAIMode.AiActive && item.DisableRequestedAt != null)
            .OrderBy(item => item.DisableRequestedAt).Take(batchSize).Select(item => item.ConversationId).ToListAsync(cancellationToken);
        forcedHandoffs.UnionWith(disabledStates);
        var inactivityStates = await db.LiveSupportAIConversationStates
            .Where(item => item.Mode == LiveSupportAIMode.AiActive && item.InactivityWarningSentAt == null && item.LastParticipantActivityAt < utcNow.AddMinutes(-30))
            .OrderBy(item => item.LastParticipantActivityAt).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var state in inactivityStates)
        {
            state.InactivityWarningSentAt = utcNow;
            state.AutoCloseAt = utcNow.AddMinutes(2);
            state.LastRecoveryAt = utcNow;
            state.Version++;
        }
        await db.SaveChangesAsync(cancellationToken);

        var reconciled = 0;
        foreach (var conversationId in forcedHandoffs.Take(batchSize))
        {
            await handoff.HandoffAsync(conversationId, null, null, "AI_RECOVERY", "تم تحويل المحادثة للدعم البشري بعد تعذر استكمال المساعد.", true, $"recovery:{conversationId:N}", cancellationToken);
            reconciled++;
        }
        LiveSupportAITelemetry.RecoveryOutcomes.Add(reconciled, new KeyValuePair<string, object?>("outcome", "reconciled"));
        return new LiveSupportAIRecoveryBatchResultDto(staleTurns.Count, expiredDecisions.Count, expiredVerifications.Count, inactivityStates.Count, reconciled);
    }
}
