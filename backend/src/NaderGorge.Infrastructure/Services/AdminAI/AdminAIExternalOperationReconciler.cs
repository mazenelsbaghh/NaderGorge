using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIExternalOperationReconciler : IAdminAIExternalOperationReconciler
{
    private readonly IAppDbContext _db; private readonly IReadOnlyDictionary<string, IAdminAIExternalResultResolver> _resolvers;
    private readonly IAdminAIAuditWriter? _audit;
    public AdminAIExternalOperationReconciler(IAppDbContext db, IEnumerable<IAdminAIExternalResultResolver> resolvers, IAdminAIAuditWriter? audit = null)
    {
        _db = db;
        _audit = audit;
        var materialized = resolvers.ToArray();
        var invalid = materialized.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.CapabilityKey));
        if (invalid is not null) throw new InvalidOperationException("External result resolvers require a capability key.");
        var duplicate = materialized.GroupBy(x => x.CapabilityKey, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Duplicate external resolver for capability '{duplicate.Key}'.");
        _resolvers = materialized.ToDictionary(x => x.CapabilityKey, StringComparer.Ordinal);
    }

    public async Task<int> ReconcileAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var pending = await _db.AdminAIActionExecutions.Where(x => x.Status == AdminAIExecutionStatus.RecoveryRequired && x.ExternalOperationId != null).OrderBy(x => x.ClaimedAt).Take(batchSize).ToListAsync(ct);
        var resolved = 0;
        foreach (var execution in pending)
        {
            if (!_resolvers.TryGetValue(execution.CapabilityKey, out var resolver)) continue;
            AdminAIActionOutcome? outcome;
            try
            {
                outcome = await resolver.ResolveAsync(execution.ExternalOperationId!, execution.Id.ToString("N"), ct);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                continue;
            }
            if (outcome is null || outcome.Status == AdminAIExecutionStatus.RecoveryRequired) continue;
            if (outcome.Status is not (AdminAIExecutionStatus.Succeeded or AdminAIExecutionStatus.PartiallySucceeded or AdminAIExecutionStatus.Rejected or AdminAIExecutionStatus.Failed))
                throw new InvalidOperationException($"External resolver returned non-terminal status '{outcome.Status}'.");
            execution.Status = outcome.Status; execution.SafeResultJson = JsonSerializer.Serialize(outcome.SafeResult); execution.AffectedCount = outcome.AffectedCount; execution.RefreshScopesJson = JsonSerializer.Serialize(outcome.RefreshScopes); execution.OriginalAuditLogId = outcome.OriginalAuditLogId; execution.CompletedAt = DateTime.UtcNow; execution.Version++;
            var proposal = await _db.AdminAIActionProposals.SingleAsync(x => x.Id == execution.ProposalId, ct);
            proposal.Status = outcome.Status switch
            {
                AdminAIExecutionStatus.Succeeded => AdminAIProposalStatus.Succeeded,
                AdminAIExecutionStatus.PartiallySucceeded => AdminAIProposalStatus.PartiallySucceeded,
                AdminAIExecutionStatus.Rejected => AdminAIProposalStatus.Rejected,
                _ => AdminAIProposalStatus.Failed
            };
            proposal.CompletedAt = DateTime.UtcNow; proposal.Version++; resolved++;
            if (_audit is not null)
                await _audit.WriteAsync(TerminalAuditEvent(outcome.Status), execution.ActorAdminUserId, proposal.ConversationId, proposal.TurnId, proposal.Id,
                    new { ExecutionId = execution.Id, execution.CapabilityKey, outcome.AffectedCount, outcome.OriginalAuditLogId, Reconciled = true }, ct);
        }
        if (resolved > 0) await _db.SaveChangesAsync(ct);
        return resolved;
    }

    private static string TerminalAuditEvent(AdminAIExecutionStatus status) => status switch
    {
        AdminAIExecutionStatus.Succeeded => "ExecutionSucceeded",
        AdminAIExecutionStatus.PartiallySucceeded => "ExecutionPartiallySucceeded",
        AdminAIExecutionStatus.Rejected => "ExecutionRejected",
        _ => "ExecutionFailed"
    };
}
