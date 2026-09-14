using System.Text;
using System.Text.Json;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIActionExecutor : IAdminAIActionExecutor
{
    private readonly IAppDbContext _db; private readonly IAdminAIAccessGate _access; private readonly IAdminAIDataProtector _protector;
    private readonly IAdminAISecureInputService _secureInputs;
    private readonly IAdminAIAuditWriter? _audit;
    private readonly IReadOnlyDictionary<string, IAdminAIActionCapability> _adapters;
    public AdminAIActionExecutor(IAppDbContext db, IAdminAIAccessGate access, IAdminAIDataProtector protector, IAdminAISecureInputService secureInputs, IEnumerable<IAdminAIActionCapability> adapters, IAdminAIAuditWriter? audit = null)
    { _db = db; _access = access; _protector = protector; _secureInputs = secureInputs; _audit = audit; _adapters = adapters.ToDictionary(x => x.Key, StringComparer.Ordinal); }

    public async Task<AdminAIExecutionResultDto> ExecuteAsync(Guid actorId, Guid proposalId, string idempotencyKey, CancellationToken ct)
    {
        await _access.RequireCurrentAdminAsync(actorId, null, ct);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("A bounded idempotency key is required.");
        var digest = _protector.Digest("action-idempotency", Encoding.UTF8.GetBytes($"{actorId:N}:{idempotencyKey}"));
        await using var transaction = await BeginSerializableIfSupportedAsync(ct);
        var proposal = await _db.AdminAIActionProposals.SingleOrDefaultAsync(x => x.Id == proposalId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        var existing = await FindExistingAsync(actorId, proposal, digest, ct);
        if (existing is not null)
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Dto(existing);
        }
        if (proposal.Status != AdminAIProposalStatus.Confirming || proposal.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Proposal is not executable.");
        if (!_adapters.TryGetValue(proposal.CapabilityKey, out var adapter)) throw new NotSupportedException("Authoritative action adapter is unavailable.");
        var plaintext = _protector.Unprotect("proposal-payload", new AdminAIProtectedValue(proposal.ProtectedNormalizedPayload, proposal.PayloadHash));
        var input = JsonSerializer.Deserialize<JsonElement>(plaintext);
        var preview = await adapter.PreviewAsync(actorId, input, ct);
        if (!StringComparer.Ordinal.Equals(preview.StateFingerprint, proposal.StateFingerprint)) { proposal.Status = AdminAIProposalStatus.Invalidated; proposal.InvalidatedReasonCode = "stale_state"; proposal.Version++; await _db.SaveChangesAsync(ct); throw new InvalidOperationException("Proposal state changed."); }
        var execution = new AdminAIActionExecution { ProposalId = proposalId, ActorAdminUserId = actorId, CapabilityKey = proposal.CapabilityKey, CapabilityVersion = proposal.CapabilityVersion, IdempotencyDigest = digest, PayloadHash = proposal.PayloadHash, AuthoritativeOperation = adapter.GetType().FullName ?? adapter.GetType().Name, Status = AdminAIExecutionStatus.Claimed, TraceId = Guid.NewGuid().ToString("N"), ClaimedAt = DateTime.UtcNow };
        // The execution id is generated client-side by BaseEntity. Persist the same
        // deterministic identity before invoking an external provider so an
        // ambiguous timeout can always be reconciled without issuing a new effect.
        execution.ExternalOperationId = execution.Id.ToString("N");
        _db.AdminAIActionExecutions.Add(execution); proposal.Status = AdminAIProposalStatus.Executing; proposal.Version++;
        if (_audit is not null)
            await _audit.WriteAsync("ExecutionStarted", actorId, proposal.ConversationId, proposal.TurnId, proposal.Id, new { ExecutionId = execution.Id, execution.CapabilityKey, AffectedCount = 0 }, ct);
        await _db.SaveChangesAsync(ct);
        byte[]? securePlaintext = null;
        if (proposal.SecureInputGrantId is not null)
        {
            if (adapter is not IAdminAISecureActionCapability secureAdapter)
                throw new InvalidOperationException("The proposal has secure input but its authoritative adapter does not accept it.");
            var grant = await _db.AdminAISecureInputGrants.AsNoTracking()
                .SingleAsync(x => x.Id == proposal.SecureInputGrantId.Value && x.ProposalId == proposalId && x.ActorAdminUserId == actorId, ct);
            if (!StringComparer.Ordinal.Equals(grant.InputKind, secureAdapter.SecureInputKind))
                throw new InvalidOperationException("Secure input kind does not match the authoritative adapter.");
            var protectedInput = await _secureInputs.ConsumeAsync(actorId, proposalId, ct);
            securePlaintext = _protector.Unprotect($"secure-input:{grant.InputKind}", protectedInput);
        }
        else if (adapter is IAdminAISecureActionCapability)
        {
            throw new InvalidOperationException("The authoritative adapter requires secure input.");
        }
        AdminAIActionOutcome outcome;
        try
        {
            outcome = adapter is IAdminAISecureActionCapability secureAdapter
                ? await secureAdapter.ExecuteSecureAsync(actorId, input, securePlaintext!, execution.ExternalOperationId, ct)
                : await adapter.ExecuteAsync(actorId, input, execution.ExternalOperationId, ct);
        }
        catch (TimeoutException)
        {
            await MarkRecoveryRequiredAsync(execution, proposal, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Dto(execution);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await MarkRecoveryRequiredAsync(execution, proposal, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Dto(execution);
        }
        finally
        {
            if (securePlaintext is not null) CryptographicOperations.ZeroMemory(securePlaintext);
        }
        execution.Status = outcome.Status; execution.SafeResultJson = JsonSerializer.Serialize(outcome.SafeResult); execution.AffectedCount = outcome.AffectedCount; execution.SucceededCount = outcome.SucceededCount; execution.SkippedCount = outcome.SkippedCount; execution.FailedCount = outcome.FailedCount; execution.RefreshScopesJson = JsonSerializer.Serialize(outcome.RefreshScopes); execution.OriginalAuditLogId = outcome.OriginalAuditLogId; execution.CompletedAt = DateTime.UtcNow; execution.Version++;
        if (outcome.Items is not null)
        {
            foreach (var item in outcome.Items)
            {
                execution.Items.Add(new AdminAIActionExecutionItem
                {
                    ItemSequence = item.Sequence,
                    SafeItemReference = item.SafeReference,
                    ItemReferenceHash = _protector.Digest("bulk-item-reference", Encoding.UTF8.GetBytes(item.SafeReference)),
                    Status = item.Status,
                    SafeResultJson = JsonSerializer.Serialize(item.SafeResult),
                    FailureCode = item.FailureCode
                });
            }
        }
        proposal.Status = outcome.Status == AdminAIExecutionStatus.Succeeded ? AdminAIProposalStatus.Succeeded : outcome.Status == AdminAIExecutionStatus.PartiallySucceeded ? AdminAIProposalStatus.PartiallySucceeded : AdminAIProposalStatus.Failed; proposal.CompletedAt = DateTime.UtcNow; proposal.Version++;
        if (_audit is not null)
            await _audit.WriteAsync(TerminalAuditEvent(outcome.Status), actorId, proposal.ConversationId, proposal.TurnId, proposal.Id, new { ExecutionId = execution.Id, execution.CapabilityKey, outcome.AffectedCount, outcome.OriginalAuditLogId }, ct);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Dto(execution);
    }

    private async Task MarkRecoveryRequiredAsync(AdminAIActionExecution execution, AdminAIActionProposal proposal, CancellationToken ct)
    {
        execution.Status = AdminAIExecutionStatus.RecoveryRequired;
        execution.FailureCode = "external_outcome_unknown";
        execution.SafeResultJson = "{}";
        execution.RefreshScopesJson = "[]";
        execution.CompletedAt = null;
        execution.Version++;
        proposal.Status = AdminAIProposalStatus.RecoveryRequired;
        proposal.CompletedAt = null;
        proposal.Version++;
        if (_audit is not null)
            await _audit.WriteAsync("ExecutionRecoveryRequired", execution.ActorAdminUserId, proposal.ConversationId, proposal.TurnId, proposal.Id, new { ExecutionId = execution.Id, execution.CapabilityKey, AffectedCount = 0, execution.FailureCode }, ct);
        await _db.SaveChangesAsync(ct);
    }

    private static string TerminalAuditEvent(AdminAIExecutionStatus status) => status switch
    {
        AdminAIExecutionStatus.Succeeded => "ExecutionSucceeded",
        AdminAIExecutionStatus.PartiallySucceeded => "ExecutionPartiallySucceeded",
        AdminAIExecutionStatus.Rejected => "ExecutionRejected",
        AdminAIExecutionStatus.RecoveryRequired => "ExecutionRecoveryRequired",
        _ => "ExecutionFailed"
    };

    private async Task<AdminAIActionExecution?> FindExistingAsync(Guid actorId, AdminAIActionProposal proposal, string digest, CancellationToken ct)
    {
        var keyed = await _db.AdminAIActionExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.ActorAdminUserId == actorId && x.IdempotencyDigest == digest, ct);
        if (keyed is not null)
        {
            if (keyed.ProposalId != proposal.Id || keyed.PayloadHash != proposal.PayloadHash) throw new InvalidOperationException("Idempotency payload conflict.");
            return keyed;
        }
        var replay = await _db.AdminAIActionExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.ProposalId == proposal.Id, ct);
        if (replay is null) return null;
        if (replay.ActorAdminUserId != actorId || replay.IdempotencyDigest != digest || replay.PayloadHash != proposal.PayloadHash) throw new InvalidOperationException("Idempotency payload conflict.");
        return replay;
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginSerializableIfSupportedAsync(CancellationToken ct)
    {
        if (_db is not DbContext context || !context.Database.IsRelational() || context.Database.CurrentTransaction is not null) return null;
        return await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    }

    private static AdminAIExecutionResultDto Dto(AdminAIActionExecution x) => new(x.Id, x.Status, x.AffectedCount, x.SucceededCount, x.SkippedCount, x.FailedCount, JsonSerializer.Deserialize<string[]>(x.RefreshScopesJson) ?? [], x.FailureCode);
}
