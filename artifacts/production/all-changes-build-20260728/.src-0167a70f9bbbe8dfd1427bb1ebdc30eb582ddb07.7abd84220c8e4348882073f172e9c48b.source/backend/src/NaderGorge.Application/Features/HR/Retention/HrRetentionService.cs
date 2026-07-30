using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Retention;

public sealed record RetentionDryRunResult(IReadOnlyList<Guid> DocumentIds, IReadOnlyList<Guid> CandidateIds, int ProtectedByLegalHold);

public sealed class HrRetentionService(IAppDbContext db)
{
    public async Task<RetentionDryRunResult> DryRunAsync(DateOnly today, int candidateRetentionYears, CancellationToken ct)
    {
        var documentIds = await db.EmployeeDocuments.AsNoTracking().Where(item => !item.IsArchived && !item.LegalHold && item.RetainUntil.HasValue && item.RetainUntil < today).Select(item => item.Id).ToListAsync(ct);
        var protectedCount = await db.EmployeeDocuments.CountAsync(item => !item.IsArchived && item.LegalHold && item.RetainUntil.HasValue && item.RetainUntil < today, ct);
        var candidateCutoff = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddYears(-candidateRetentionYears);
        var candidateIds = await db.Candidates.AsNoTracking().Where(item => !item.EmployeeProfileId.HasValue &&
            (item.Stage == Domain.Enums.CandidateStage.Rejected || item.Stage == Domain.Enums.CandidateStage.Withdrawn) && item.CreatedAt < candidateCutoff).Select(item => item.Id).ToListAsync(ct);
        return new RetentionDryRunResult(documentIds, candidateIds, protectedCount);
    }

    public async Task<RetentionDryRunResult> ExecuteAsync(DateOnly today, int candidateRetentionYears, Guid actorUserId, string reason, CancellationToken ct)
    {
        var result = await DryRunAsync(today, candidateRetentionYears, ct);
        foreach (var document in await db.EmployeeDocuments.Where(item => result.DocumentIds.Contains(item.Id)).ToListAsync(ct)) document.IsArchived = true;
        foreach (var candidate in await db.Candidates.Where(item => result.CandidateIds.Contains(item.Id)).ToListAsync(ct))
        { candidate.FullName = $"ANON-{candidate.Id:N}"; candidate.PhoneNumber = $"ANON-{candidate.Id:N}"; candidate.Email = null; candidate.CvAssetReference = null; candidate.Version++; }
        db.AuditLogs.Add(new AuditLog { Action = "ExecuteHrRetention", EntityType = "HrRetentionBatch", PerformedByUserId = actorUserId,
            ActorSnapshot = actorUserId.ToString(), Reason = reason.Trim(), NewValues = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct); return result;
    }
}
