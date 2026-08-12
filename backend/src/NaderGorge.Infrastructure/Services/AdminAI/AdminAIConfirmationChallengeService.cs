using System.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIConfirmationChallengeService(IAppDbContext db, IAdminAIDataProtector protector) : IAdminAIConfirmationChallengeService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<string> IssueAsync(Guid actorId, Guid proposalId, string safeActionLabel, CancellationToken ct)
    {
        var proposal = await db.AdminAIActionProposals.SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (proposal.ConfirmationType != AdminAIConfirmationType.TypedStrong || proposal.Status != AdminAIProposalStatus.PendingConfirmation || proposal.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Strong confirmation is unavailable.");
        if (await db.AdminAIConfirmationChallenges.AnyAsync(x => x.ProposalId == proposalId, ct)) throw new InvalidOperationException("Challenge already issued.");
        var phrase = Phrase(proposal, proposal.CapabilityKey);
        db.AdminAIConfirmationChallenges.Add(new AdminAIConfirmationChallenge { ProposalId = proposalId, PhraseDigest = Digest(phrase), ExpiresAt = proposal.ExpiresAt });
        await db.SaveChangesAsync(ct); return phrase;
    }

    public async Task<string?> PhraseAsync(Guid actorId, Guid proposalId, CancellationToken ct)
    {
        var proposal = await db.AdminAIActionProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (proposal.ConfirmationType != AdminAIConfirmationType.TypedStrong || proposal.Status != AdminAIProposalStatus.PendingConfirmation || proposal.ExpiresAt <= DateTime.UtcNow) return null;
        var exists = await db.AdminAIConfirmationChallenges.AsNoTracking().AnyAsync(x => x.ProposalId == proposalId && x.Status == AdminAIChallengeStatus.Pending, ct);
        return exists ? Phrase(proposal, proposal.CapabilityKey) : null;
    }

    public async Task<bool> VerifyAsync(Guid actorId, Guid proposalId, string phrase, CancellationToken ct)
    {
        var proposal = await db.AdminAIActionProposals.SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        var challenge = await db.AdminAIConfirmationChallenges.SingleOrDefaultAsync(x => x.ProposalId == proposalId, ct) ?? throw new KeyNotFoundException();
        if (challenge.Status != AdminAIChallengeStatus.Pending || proposal.Status != AdminAIProposalStatus.PendingConfirmation) return false;
        if (challenge.ExpiresAt <= DateTime.UtcNow || proposal.ExpiresAt <= DateTime.UtcNow)
        {
            challenge.Status = AdminAIChallengeStatus.Expired; challenge.Version++;
            proposal.Status = AdminAIProposalStatus.Expired; proposal.Version++;
            await db.SaveChangesAsync(ct); return false;
        }
        challenge.LastAttemptAt = DateTime.UtcNow; challenge.Version++;
        var supplied = Convert.FromHexString(Digest(phrase)); var expected = Convert.FromHexString(challenge.PhraseDigest);
        if (!CryptographicOperations.FixedTimeEquals(supplied, expected))
        {
            challenge.FailedAttemptCount++;
            if (challenge.FailedAttemptCount >= 5) { challenge.Status = AdminAIChallengeStatus.Locked; proposal.Status = AdminAIProposalStatus.Invalidated; proposal.InvalidatedReasonCode = "strong_confirmation_locked"; proposal.Version++; }
            await db.SaveChangesAsync(ct); return false;
        }
        challenge.Status = AdminAIChallengeStatus.Accepted; challenge.AcceptedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return true;
    }

    private string Digest(string phrase) => protector.Digest("strong-confirmation", Encoding.UTF8.GetBytes(protector.NormalizeConfirmationPhrase(phrase)));
    private string Phrase(AdminAIActionProposal proposal, string label)
    {
        var seed = Convert.FromHexString(protector.Digest("strong-challenge-seed", proposal.Id.ToByteArray()));
        var challenge = new string(seed.Take(8).Select(value => Alphabet[value % Alphabet.Length]).ToArray());
        return $"أؤكد تنفيذ {label} — {challenge}";
    }
}
