using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIRecoveryService(IAppDbContext db) : IAdminAIRecoveryService
{
    public async Task<int> ReconcileAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now = DateTime.UtcNow;
        var changed = 0;
        var cancelledTurns = await db.AdminAITurns
            .Where(x => x.CancellationRequestedAt != null && x.Status != AdminAITurnStatus.Completed && x.Status != AdminAITurnStatus.Cancelled && x.Status != AdminAITurnStatus.Failed && x.Status != AdminAITurnStatus.AccessRevoked)
            .OrderBy(x => x.CancellationRequestedAt).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var turn in cancelledTurns)
        {
            turn.Status = AdminAITurnStatus.Cancelled;
            turn.FailureCode = "CANCELLED";
            turn.CompletedAt = now;
            turn.Version++;
            changed++;
        }
        var remaining = batchSize - changed;
        var reads = remaining == 0 ? [] : await db.AdminAIReadInvocations.Where(x => x.ProtectedResult != null && x.ProtectedResultExpiresAt <= now).OrderBy(x => x.ProtectedResultExpiresAt).Take(remaining).ToListAsync(cancellationToken);
        foreach (var read in reads) { read.ProtectedResult = null; read.ProtectedResultHash = null; read.ProtectedResultExpiresAt = null; changed++; }
        remaining = batchSize - changed;
        var grants = remaining == 0 ? [] : await db.AdminAISecureInputGrants.Where(x => x.ProtectedPayload != null && x.ExpiresAt <= now).OrderBy(x => x.ExpiresAt).Take(remaining).ToListAsync(cancellationToken);
        foreach (var grant in grants) { grant.ProtectedPayload = null; grant.PayloadHash = null; grant.Status = AdminAISecureInputGrantStatus.Expired; grant.PurgedAt = now; grant.Version++; changed++; }
        remaining = batchSize - changed;
        var proposals = await db.AdminAIActionProposals
            .Where(x => (x.Status == AdminAIProposalStatus.PendingConfirmation || x.Status == AdminAIProposalStatus.PendingSecureInput) && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt).Take(remaining).ToListAsync(cancellationToken);
        foreach (var proposal in proposals)
        {
            proposal.Status = AdminAIProposalStatus.Expired;
            proposal.Version++;
            changed++;
            var challenge = await db.AdminAIConfirmationChallenges.SingleOrDefaultAsync(x => x.ProposalId == proposal.Id && x.Status == AdminAIChallengeStatus.Pending, cancellationToken);
            if (challenge is not null) { challenge.Status = AdminAIChallengeStatus.Expired; challenge.Version++; }
        }

        remaining = batchSize - changed;
        var revokedTurns = remaining == 0 ? [] : await db.AdminAITurns
            .Where(turn => turn.Status != AdminAITurnStatus.Completed && turn.Status != AdminAITurnStatus.Cancelled && turn.Status != AdminAITurnStatus.Failed && turn.Status != AdminAITurnStatus.AccessRevoked &&
                !db.AdminAITurnSteps.Any(step => step.TurnId == turn.Id && step.CallbackStatus == "Pending" && step.NextCallbackAttemptAt <= now && step.CallbackAttemptCount >= 5) &&
                !db.Users.Any(user => user.Id == turn.ActorAdminUserId && user.IsActive && !user.IsDeleted && user.UserRoles.Any(link => link.Role.Type == RoleType.Admin)))
            .OrderBy(x => x.QueuedAt).Take(remaining).ToListAsync(cancellationToken);
        foreach (var turn in revokedTurns) { turn.Status = AdminAITurnStatus.AccessRevoked; turn.FailureCode = "admin_ai_access_revoked"; turn.CompletedAt = now; turn.Version++; changed++; }
        remaining = batchSize - changed;
        var staleQueuedTurns = remaining == 0 ? [] : await db.AdminAITurns
            .Where(turn => turn.Status == AdminAITurnStatus.Queued && turn.QueuedAt < now.AddMinutes(-2))
            .OrderBy(turn => turn.QueuedAt).Take(remaining).ToListAsync(cancellationToken);
        foreach (var turn in staleQueuedTurns)
        {
            turn.Status = AdminAITurnStatus.Failed;
            turn.FailureCode = "admin_ai_queue_stale";
            turn.CompletedAt = now;
            turn.Version++;
            changed++;
        }
        remaining = batchSize - changed;
        var staleSteps = remaining == 0 ? [] : await db.AdminAITurnSteps
            .Where(x => (x.Status == AdminAITurnStepStatus.Claimed ||
                         x.Status == AdminAITurnStepStatus.ProviderRunning ||
                         x.Status == AdminAITurnStepStatus.ReadsCompleted) &&
                        x.StartedAt != null && x.StartedAt < now.AddMinutes(-2))
            .OrderBy(x => x.StartedAt).Take(remaining).ToListAsync(cancellationToken);
        foreach (var step in staleSteps)
        {
            step.Status = AdminAITurnStepStatus.Failed;
            step.FailureCode = "admin_ai_worker_lease_expired";
            step.CompletedAt = now;
            step.Version++;
            var turn = await db.AdminAITurns.SingleOrDefaultAsync(x => x.Id == step.TurnId && x.Status != AdminAITurnStatus.Completed && x.Status != AdminAITurnStatus.Cancelled && x.Status != AdminAITurnStatus.Failed && x.Status != AdminAITurnStatus.AccessRevoked, cancellationToken);
            if (turn is not null) { turn.Status = AdminAITurnStatus.Failed; turn.FailureCode = "admin_ai_worker_lease_expired"; turn.CompletedAt = now; turn.Version++; }
            changed++;
        }

        remaining = batchSize - changed;
        var exhaustedCallbacks = remaining == 0 ? [] : await db.AdminAITurnSteps
            .Where(x => x.CallbackStatus == "Pending" && x.NextCallbackAttemptAt <= now && x.CallbackAttemptCount >= 5 && x.Status != AdminAITurnStepStatus.Completed)
            .OrderBy(x => x.NextCallbackAttemptAt).Take(remaining).ToListAsync(cancellationToken);
        foreach (var step in exhaustedCallbacks)
        {
            step.CallbackStatus = "Failed";
            step.Status = AdminAITurnStepStatus.Failed;
            step.FailureCode = "CALLBACK_UNAVAILABLE";
            step.CompletedAt = now;
            step.Version++;
            var turn = await db.AdminAITurns.SingleOrDefaultAsync(x => x.Id == step.TurnId && x.Status != AdminAITurnStatus.Completed && x.Status != AdminAITurnStatus.Cancelled && x.Status != AdminAITurnStatus.Failed && x.Status != AdminAITurnStatus.AccessRevoked, cancellationToken);
            if (turn is not null)
            {
                turn.Status = AdminAITurnStatus.Failed;
                turn.FailureCode = "CALLBACK_UNAVAILABLE";
                turn.CompletedAt = now;
                turn.Version++;
            }
            changed++;
        }

        if (changed > 0) await db.SaveChangesAsync(cancellationToken);
        return changed;
    }
}
