using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Commands;

public sealed class AdminAIProposalCommands(IAppDbContext db, IAdminAIAccessGate access, IAdminAIConfirmationChallengeService challenges, IAdminAIActionExecutor executor)
{
    public async Task<AdminAIExecutionResultDto> ConfirmAsync(Guid actorId, Guid proposalId, long expectedVersion, string? phrase, string idempotencyKey, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        var proposal = await db.AdminAIActionProposals.SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (proposal.Status is AdminAIProposalStatus.Succeeded or AdminAIProposalStatus.PartiallySucceeded or AdminAIProposalStatus.Failed or AdminAIProposalStatus.Executing)
            return await executor.ExecuteAsync(actorId, proposalId, idempotencyKey, ct);
        if (proposal.Version != expectedVersion || proposal.Status != AdminAIProposalStatus.PendingConfirmation) throw new InvalidOperationException("Proposal is stale or unavailable.");
        if (proposal.ExpiresAt <= DateTime.UtcNow) { proposal.Status = AdminAIProposalStatus.Expired; proposal.Version++; await PurgeGrantAsync(proposalId, AdminAISecureInputGrantStatus.Expired, ct); await db.SaveChangesAsync(ct); throw new InvalidOperationException("Proposal expired."); }
        var ownsConversation = await db.AdminAIConversations.AsNoTracking().AnyAsync(x => x.Id == proposal.ConversationId && x.OwnerAdminUserId == actorId, ct);
        var baselineCurrent = await db.AdminAICapabilityBaselines.AsNoTracking().AnyAsync(x => x.Id == proposal.CapabilityBaselineId && x.Status == AdminAICapabilityBaselineStatus.Active, ct);
        var policyCurrent = await db.AdminAISensitiveDataPolicyVersions.AsNoTracking().AnyAsync(x => x.Id == proposal.SensitiveDataPolicyVersionId && x.Status == AdminAISensitiveDataPolicyStatus.Active, ct);
        if (!ownsConversation || !baselineCurrent || !policyCurrent) { proposal.Status = AdminAIProposalStatus.Invalidated; proposal.InvalidatedReasonCode = "governance_or_owner_changed"; proposal.Version++; await db.SaveChangesAsync(ct); throw new UnauthorizedAccessException("Proposal authorization changed."); }
        if (proposal.ConfirmationType == AdminAIConfirmationType.TypedStrong && (phrase is null || !await challenges.VerifyAsync(actorId, proposalId, phrase, ct))) throw new UnauthorizedAccessException("Strong confirmation failed.");
        proposal.Status = AdminAIProposalStatus.Confirming; proposal.ConfirmedAt = DateTime.UtcNow; proposal.Version++; await db.SaveChangesAsync(ct);
        return await executor.ExecuteAsync(actorId, proposalId, idempotencyKey, ct);
    }

    public async Task<AdminAIProposalDto> CancelAsync(Guid actorId, Guid proposalId, long expectedVersion, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        var proposal = await db.AdminAIActionProposals.SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (proposal.Status == AdminAIProposalStatus.Cancelled) return Dto(proposal);
        if (proposal.Version != expectedVersion || proposal.Status is AdminAIProposalStatus.Executing or AdminAIProposalStatus.Succeeded or AdminAIProposalStatus.PartiallySucceeded) throw new InvalidOperationException("Proposal cannot be cancelled.");
        proposal.Status = AdminAIProposalStatus.Cancelled; proposal.CancelledAt = DateTime.UtcNow; proposal.Version++;
        var challenge = await db.AdminAIConfirmationChallenges.SingleOrDefaultAsync(x => x.ProposalId == proposalId, ct);
        if (challenge is not null && challenge.Status == AdminAIChallengeStatus.Pending) { challenge.Status = AdminAIChallengeStatus.Cancelled; challenge.Version++; }
        await PurgeGrantAsync(proposalId, AdminAISecureInputGrantStatus.Cancelled, ct);
        await db.SaveChangesAsync(ct); return Dto(proposal);
    }

    public async Task<AdminAIProposalDto> GetAsync(Guid actorId, Guid proposalId, CancellationToken ct)
    { await access.RequireCurrentAdminAsync(actorId, null, ct); var proposal = await db.AdminAIActionProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException(); return Dto(proposal, await challenges.PhraseAsync(actorId, proposalId, ct)); }

    private static AdminAIProposalDto Dto(NaderGorge.Domain.Entities.AdminAI.AdminAIActionProposal p, string? phrase = null) => new(p.Id, p.CapabilityKey, p.SafeTargetType, p.SafeTargetReference, p.PrimaryRisk, p.ConfirmationType, p.SafeCurrentStateJson, p.SafeRequestedStateJson, p.SafeEffectJson, p.ExpiresAt, p.Status, p.Version, phrase);

    private async Task PurgeGrantAsync(Guid proposalId, AdminAISecureInputGrantStatus status, CancellationToken ct)
    {
        var grant = await db.AdminAISecureInputGrants.SingleOrDefaultAsync(x => x.ProposalId == proposalId, ct);
        if (grant is null || grant.Status is AdminAISecureInputGrantStatus.Consumed or AdminAISecureInputGrantStatus.Purged) return;
        grant.ProtectedPayload = null; grant.PayloadHash = null; grant.Status = status; grant.PurgedAt = DateTime.UtcNow; grant.Version++;
    }
}
