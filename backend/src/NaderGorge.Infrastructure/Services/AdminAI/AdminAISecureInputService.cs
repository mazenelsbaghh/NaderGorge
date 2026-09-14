using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAISecureInputService(IAppDbContext db, IAdminAIAccessGate access, IAdminAIDataProtector protector, IConfiguration configuration) : IAdminAISecureInputService
{
    private static readonly HashSet<string> InputKinds = new(StringComparer.Ordinal) { "Password", "ProtectedToken", "VerificationAnswer", "PrivateFile" };

    public async Task<AdminAISecureGrantResult> IssueAsync(Guid actorId, Guid proposalId, string inputKind, long expectedProposalVersion, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        if (!InputKinds.Contains(inputKind)) throw new ArgumentException("Unsupported secure input kind.", nameof(inputKind));
        var proposal = await db.AdminAIActionProposals.SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        var token = GrantToken(actorId, proposalId, inputKind);
        var existing = await db.AdminAISecureInputGrants.AsNoTracking().SingleOrDefaultAsync(x => x.ProposalId == proposalId && x.ActorAdminUserId == actorId, ct);
        if (existing is not null)
        {
            if (!StringComparer.Ordinal.Equals(existing.InputKind, inputKind) || existing.Status != AdminAISecureInputGrantStatus.Issued || existing.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Secure grant idempotency conflict.");
            return Result(existing, token);
        }
        if (proposal.Version != expectedProposalVersion || proposal.Status is not (AdminAIProposalStatus.PendingSecureInput or AdminAIProposalStatus.PendingConfirmation) || proposal.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Proposal cannot accept secure input.");
        var ttl = Math.Clamp(configuration.GetValue("AdminAI:SecureInputTtlSeconds", 300), 30, 600);
        var grant = new AdminAISecureInputGrant { ProposalId = proposalId, ActorAdminUserId = actorId, InputKind = inputKind, TokenDigest = TokenDigest(token), ExpiresAt = DateTime.UtcNow.AddSeconds(ttl) };
        db.AdminAISecureInputGrants.Add(grant); proposal.SecureInputGrantId = grant.Id; proposal.Status = AdminAIProposalStatus.PendingSecureInput; proposal.Version++;
        await db.SaveChangesAsync(ct); return Result(grant, token);
    }

    public async Task<AdminAISecureGrantResult> SubmitAsync(Guid actorId, Guid grantId, string token, string inputKind, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        ValidatePayload(inputKind, payload.Span);
        var grant = await db.AdminAISecureInputGrants.SingleOrDefaultAsync(x => x.Id == grantId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (!StringComparer.Ordinal.Equals(grant.InputKind, inputKind)) throw new InvalidOperationException("Secure input kind mismatch.");
        if (grant.ExpiresAt <= DateTime.UtcNow) { ExpireAndPurge(grant); await db.SaveChangesAsync(ct); throw new AdminAISecureInputGoneException(); }
        if (!FixedEquals(grant.TokenDigest, TokenDigest(token))) throw new InvalidOperationException("Secure grant is invalid or expired.");
        if (grant.Status == AdminAISecureInputGrantStatus.Submitted && grant.ProtectedPayload is not null && grant.PayloadHash is not null)
        {
            var existingPayload = protector.Unprotect($"secure-input:{grant.InputKind}", new AdminAIProtectedValue(grant.ProtectedPayload, grant.PayloadHash));
            if (!CryptographicOperations.FixedTimeEquals(existingPayload, payload.Span)) throw new InvalidOperationException("Secure input idempotency conflict.");
            return Result(grant, null);
        }
        if (grant.Status != AdminAISecureInputGrantStatus.Issued) throw new AdminAISecureInputGoneException();
        var protectedPayload = protector.Protect($"secure-input:{grant.InputKind}", payload.Span);
        grant.ProtectedPayload = protectedPayload.Ciphertext; grant.PayloadHash = protectedPayload.Digest; grant.Status = AdminAISecureInputGrantStatus.Submitted; grant.SubmittedAt = DateTime.UtcNow; grant.Version++;
        var proposal = await db.AdminAIActionProposals.SingleAsync(x => x.Id == grant.ProposalId, ct); proposal.Status = AdminAIProposalStatus.PendingConfirmation; proposal.Version++;
        await db.SaveChangesAsync(ct); return Result(grant, null);
    }

    public async Task<AdminAIProtectedValue> ConsumeAsync(Guid actorId, Guid proposalId, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        var grant = await db.AdminAISecureInputGrants.SingleOrDefaultAsync(x => x.ProposalId == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        if (grant.ExpiresAt <= DateTime.UtcNow) { ExpireAndPurge(grant); await db.SaveChangesAsync(ct); throw new AdminAISecureInputGoneException(); }
        if (grant.Status is AdminAISecureInputGrantStatus.Consumed or AdminAISecureInputGrantStatus.Expired or AdminAISecureInputGrantStatus.Purged || grant.ProtectedPayload is null || grant.PayloadHash is null) throw new AdminAISecureInputGoneException();
        var value = new AdminAIProtectedValue(grant.ProtectedPayload, grant.PayloadHash);
        grant.ProtectedPayload = null; grant.PayloadHash = null; grant.Status = AdminAISecureInputGrantStatus.Consumed; grant.ConsumedAt = DateTime.UtcNow; grant.PurgedAt = DateTime.UtcNow; grant.Version++;
        await db.SaveChangesAsync(ct); return value;
    }

    private string TokenDigest(string token) => protector.Digest("secure-grant-token", Encoding.UTF8.GetBytes(token));
    private string GrantToken(Guid actorId, Guid proposalId, string kind) => protector.Digest("secure-grant-value", Encoding.UTF8.GetBytes($"{actorId:N}:{proposalId:N}:{kind}"));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
    private static AdminAISecureGrantResult Result(AdminAISecureInputGrant grant, string? token) => new(grant.Id, token, grant.InputKind, grant.Status, grant.ExpiresAt, grant.Version);
    private static void ExpireAndPurge(AdminAISecureInputGrant grant)
    { grant.ProtectedPayload = null; grant.PayloadHash = null; grant.Status = AdminAISecureInputGrantStatus.Expired; grant.PurgedAt = DateTime.UtcNow; grant.Version++; }
    private static void ValidatePayload(string kind, ReadOnlySpan<byte> payload)
    {
        var maximum = kind switch { "Password" => 512, "ProtectedToken" => 4096, "VerificationAnswer" or "PrivateFile" => 1000, _ => throw new ArgumentException("Unsupported secure input kind.", nameof(kind)) };
        var minimum = kind == "Password" ? 8 : 1;
        if (payload.Length < minimum || payload.Length > maximum) throw new ArgumentOutOfRangeException(nameof(payload));
        if (kind == "PrivateFile" && !Encoding.UTF8.GetString(payload).StartsWith("private:", StringComparison.Ordinal)) throw new ArgumentException("A private object reference is required.", nameof(payload));
    }
}
