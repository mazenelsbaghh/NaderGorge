using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public enum AdminAIBulkExecutionMode { Atomic, Partial }

public sealed record AdminAIBulkActionInput(
    JsonElement Selector,
    IReadOnlyList<string> ExcludedItemReferences,
    AdminAIBulkExecutionMode Mode);

public sealed record AdminAIBulkCandidate(string SafeReference, string VersionToken);

public sealed record AdminAIBulkMembership(
    IReadOnlyList<AdminAIBulkCandidate> Candidates,
    DateTime DataAsOf);

public sealed record AdminAIBulkItemOutcome(
    AdminAIExecutionItemStatus Status,
    object SafeResult,
    string? FailureCode = null);

/// <summary>Resolves a bounded selector through an authoritative application query.</summary>
public interface IAdminAIBulkMembershipSource
{
    Task<AdminAIBulkMembership> ResolveAsync(
        string capabilityKey,
        Guid actorId,
        JsonElement selector,
        CancellationToken cancellationToken);
}

/// <summary>Invokes the original authoritative command once for one frozen member.</summary>
public interface IAdminAIBulkItemOperation
{
    string CapabilityKey { get; }
    Task<AdminAIBulkItemOutcome> ExecuteAsync(
        Guid actorId,
        string safeItemReference,
        string operationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Shared bulk adapter. Membership and versions are included in the proposal
/// fingerprint and are resolved again immediately before execution. Atomic mode
/// uses the ambient application DbContext transaction; partial mode records a
/// terminal, safe outcome for every selected item.
/// </summary>
public sealed class AdminAIBulkActionExecutor(
    string capabilityKey,
    IAppDbContext db,
    IAdminAIBulkMembershipSource membershipSource,
    IAdminAIBulkItemOperation operation) : IAdminAIActionCapability
{
    private const int MaximumCandidates = 10_000;

    public string Key { get; } = operation.CapabilityKey == capabilityKey
        ? capabilityKey
        : throw new ArgumentException("Bulk operation capability key mismatch.", nameof(operation));

    public async Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct)
    {
        var request = Deserialize(input);
        var selection = await ResolveSelectionAsync(actorId, request, ct);
        var fingerprint = Fingerprint(request, selection);
        var sample = selection.Take(10).Select(x => x.SafeReference).ToArray();

        return new AdminAIActionPreview(
            "bulk-selection",
            fingerprint[..16],
            new { candidateCount = selection.Count, excludedCount = request.ExcludedItemReferences.Count },
            new { selector = request.Selector, mode = request.Mode.ToString(), selectedCount = selection.Count },
            new { semantics = request.Mode.ToString(), selectedCount = selection.Count, sample },
            new { exactMembership = true, maximumCandidates = MaximumCandidates },
            fingerprint);
    }

    public async Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 200)
            throw new ArgumentException("A bounded authoritative operation id is required.", nameof(operationId));

        var request = Deserialize(input);
        var selection = await ResolveSelectionAsync(actorId, request, ct);
        return request.Mode == AdminAIBulkExecutionMode.Atomic
            ? await ExecuteAtomicAsync(actorId, selection, operationId, ct)
            : await ExecutePartialAsync(actorId, selection, operationId, ct);
    }

    private async Task<AdminAIActionOutcome> ExecuteAtomicAsync(
        Guid actorId,
        IReadOnlyList<AdminAIBulkCandidate> selection,
        string operationId,
        CancellationToken ct)
    {
        if (db is not DbContext context || !context.Database.IsRelational())
            throw new InvalidOperationException("Atomic bulk execution requires a relational authoritative transaction.");

        await using var transaction = context.Database.CurrentTransaction is null
            ? await db.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var results = new List<AdminAIActionItemEvidence>(selection.Count);
        try
        {
            for (var index = 0; index < selection.Count; index++)
            {
                var candidate = selection[index];
                var outcome = await operation.ExecuteAsync(actorId, candidate.SafeReference, ItemOperationId(operationId, index), ct);
                if (outcome.Status != AdminAIExecutionItemStatus.Succeeded)
                    throw new AdminAIBulkAtomicRejectedException(outcome.FailureCode ?? "bulk_item_rejected");
                results.Add(new AdminAIActionItemEvidence(index, candidate.SafeReference, outcome.Status, outcome.SafeResult, outcome.FailureCode));
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }

        return new AdminAIActionOutcome(
            AdminAIExecutionStatus.Succeeded,
            new { mode = "Atomic", items = results.Select(SafeItem), succeeded = results.Count, skipped = 0, failed = 0 },
            results.Count,
            ["bulk", Key],
            SucceededCount: results.Count,
            SkippedCount: 0,
            FailedCount: 0,
            Items: results);
    }

    private async Task<AdminAIActionOutcome> ExecutePartialAsync(
        Guid actorId,
        IReadOnlyList<AdminAIBulkCandidate> selection,
        string operationId,
        CancellationToken ct)
    {
        var results = new List<AdminAIActionItemEvidence>(selection.Count);
        var succeeded = 0;
        var skipped = 0;
        var failed = 0;
        for (var index = 0; index < selection.Count; index++)
        {
            var candidate = selection[index];
            AdminAIBulkItemOutcome outcome;
            try
            {
                outcome = await operation.ExecuteAsync(actorId, candidate.SafeReference, ItemOperationId(operationId, index), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                outcome = new AdminAIBulkItemOutcome(AdminAIExecutionItemStatus.SystemFailed, new { }, "authoritative_operation_failed");
            }

            if (outcome.Status == AdminAIExecutionItemStatus.Succeeded) succeeded++;
            else if (outcome.Status == AdminAIExecutionItemStatus.Skipped) skipped++;
            else failed++;
            results.Add(new AdminAIActionItemEvidence(index, candidate.SafeReference, outcome.Status, outcome.SafeResult, outcome.FailureCode));
        }

        var status = failed == 0
            ? AdminAIExecutionStatus.Succeeded
            : succeeded == 0 ? AdminAIExecutionStatus.Rejected : AdminAIExecutionStatus.PartiallySucceeded;
        return new AdminAIActionOutcome(
            status,
            new { mode = "Partial", items = results.Select(SafeItem), selected = selection.Count, succeeded, skipped, failed },
            succeeded,
            ["bulk", Key],
            SucceededCount: succeeded,
            SkippedCount: skipped,
            FailedCount: failed,
            Items: results);
    }

    private async Task<IReadOnlyList<AdminAIBulkCandidate>> ResolveSelectionAsync(Guid actorId, AdminAIBulkActionInput request, CancellationToken ct)
    {
        var membership = await membershipSource.ResolveAsync(Key, actorId, request.Selector, ct);
        if (membership.Candidates.Count > MaximumCandidates)
            throw new InvalidOperationException("Bulk selector exceeds the reviewed candidate limit.");
        var duplicates = membership.Candidates.GroupBy(x => x.SafeReference, StringComparer.Ordinal).Any(x => x.Count() > 1);
        if (duplicates) throw new InvalidOperationException("Bulk selector returned duplicate membership.");
        var excluded = request.ExcludedItemReferences.ToHashSet(StringComparer.Ordinal);
        return membership.Candidates
            .Where(x => !excluded.Contains(x.SafeReference))
            .OrderBy(x => x.SafeReference, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Fingerprint(AdminAIBulkActionInput request, IReadOnlyList<AdminAIBulkCandidate> candidates)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            selector = request.Selector,
            exclusions = request.ExcludedItemReferences.Order(StringComparer.Ordinal),
            mode = request.Mode.ToString(),
            membership = candidates.Select(x => new { x.SafeReference, x.VersionToken })
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ItemOperationId(string operationId, int index) => $"{operationId}:{index:D6}";

    private static object SafeItem(AdminAIActionItemEvidence item) => new
    {
        reference = item.SafeReference,
        status = item.Status.ToString(),
        item.SafeResult,
        item.FailureCode
    };

    private static AdminAIBulkActionInput Deserialize(object input)
    {
        if (input is AdminAIBulkActionInput typed) return typed;
        var result = input is JsonElement json
            ? json.Deserialize<AdminAIBulkActionInput>()
            : JsonSerializer.Deserialize<AdminAIBulkActionInput>(JsonSerializer.Serialize(input));
        return result ?? throw new ArgumentException("Bulk action input is empty.", nameof(input));
    }
}

public sealed class AdminAIBulkAtomicRejectedException(string failureCode)
    : InvalidOperationException("Atomic bulk operation was rejected by an authoritative item operation.")
{
    public string FailureCode { get; } = failureCode;
}
