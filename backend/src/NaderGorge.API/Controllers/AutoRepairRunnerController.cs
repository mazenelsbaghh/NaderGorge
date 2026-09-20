using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.AutoRepair;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/internal/auto-repair")]
[AllowAnonymous]
[ServiceFilter(typeof(RepairRunnerAuth))]
public sealed class AutoRepairRunnerController(AppDbContext db, RepairStore store) : ControllerBase
{
    [HttpPost("logs")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> Logs(RepairStore.RepairLog[] logs, CancellationToken ct)
    {
        if (logs.Length > 200) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1700914)", ct);
        await store.IngestExternal(logs, ct);
        await tx.CommitAsync(ct);
        return Ok(new { accepted = logs.Length });
    }

    [HttpPost("synchronization")]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Synchronization(RepairSynchronization.Snapshot request, CancellationToken ct)
    {
        if (!RepairSynchronization.Valid(request)) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1700914)", ct);
        await RepairSynchronization.Record(db, request, ct);
        await tx.CommitAsync(ct);
        return Ok(new { accepted = true });
    }

    [HttpPost("claim")]
    public async Task<IActionResult> Claim(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1700914)", ct);
        await store.ConsolidateQueued(ct);
        await store.Ingest(ct);
        await store.CollectEvidence(ct);
        var control = await db.AutoRepairControls.SingleAsync(ct);
        control.Heartbeat = DateTimeOffset.UtcNow;
        control.Runner = "node-3";
        var expired = await db.AutoRepairIncidents.Where(x => x.LeaseToken != null && x.LeaseUntil < DateTimeOffset.UtcNow).ToListAsync(ct);
        foreach (var stale in expired)
        {
            control.Paused = true;
            stale.Status = "failed";
            stale.LeaseToken = null;
            RepairStore.Event(stale, "انتهى اتصال المنفذ؛ يلزم مراجعة الحالة الفعلية قبل إعادة المحاولة", "recovery");
        }
        var active = await db.AutoRepairIncidents.AnyAsync(x => x.LeaseUntil > DateTimeOffset.UtcNow && x.LeaseToken != null, ct);
        var synchronized = RepairSynchronization.AllowsClaim(await RepairSynchronization.Latest(db, ct));
        var incident = control.Paused || active || !synchronized ? null : await db.AutoRepairIncidents
            .Where(x => (x.Status == "queued" && x.Attempts < 3) || (x.Status == "ready" && (control.AutoDeploy || x.ApprovedHash == x.ProposalHash)))
            .OrderBy(x => x.FirstSeen).FirstOrDefaultAsync(ct);
        if (incident is not null)
        {
            incident.LeaseToken = Guid.NewGuid();
            incident.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(3);
            if (incident.Status == "queued") { incident.Status = "diagnosing"; incident.Attempts++; }
            RepairStore.Event(incident, "استلم المنفذ الحالة", "node-3");
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var evidence = incident is null ? null : await store.DiagnosticEvidence(incident, ct);
        return Ok(new { incident = incident is null ? null : new { incident.Id, incident.Status, Evidence = evidence, incident.Category, incident.LeaseToken, incident.ProposalHash, incident.ApprovedHash }, control.AutoDeploy });
    }

    [HttpPost("{id:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid id, LeaseRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1700914)", ct);
        var incident = await db.AutoRepairIncidents.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null || incident.LeaseToken != request.LeaseToken || incident.LeaseUntil <= DateTimeOffset.UtcNow) return Conflict();
        await store.Ingest(ct);
        await store.CollectEvidence(ct);
        var control = await db.AutoRepairControls.SingleAsync(ct);
        incident.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(3);
        control.Heartbeat = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { control.Paused, control.AutoDeploy, incident.Occurrences });
    }

    [HttpPost("{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, ReportRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1700914)", ct);
        var incident = await db.AutoRepairIncidents.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null || incident.LeaseToken != request.LeaseToken || incident.LeaseUntil <= DateTimeOffset.UtcNow) return Conflict();
        if (incident.Status != request.Status && !RepairPolicy.CanTransition(incident.Status, request.Status)) return Conflict();
        if (request.Status == "needs_evidence" && (string.IsNullOrWhiteSpace(request.Detail) || request.Detail.Length > 12_000)) return BadRequest();
        var control = await db.AutoRepairControls.SingleAsync(ct);
        if (request.Status == "deploying" && (control.Paused || (!control.AutoDeploy && incident.ApprovedHash != incident.ProposalHash))) return Conflict();
        if (request.ProposalHash is not null && !System.Text.RegularExpressions.Regex.IsMatch(request.ProposalHash, "^[a-f0-9]{64}$")) return BadRequest();
        if (request.Status is "ready" or "awaiting_approval" && request.ProposalHash is null) return BadRequest();
        if (request.ProposalHash is not null && incident.ProposalHash.Length > 0 && request.ProposalHash != incident.ProposalHash) incident.ApprovedHash = "";
        var previousStatus = incident.Status;
        if (request.Status is "failed" or "rolled_back" && previousStatus is "deploying" or "monitoring") control.Paused = true;
        incident.Status = request.Status;
        incident.Summary = RepairPolicy.Redact(request.Detail);
        if (request.ProposalHash is not null) incident.ProposalHash = request.ProposalHash;
        if (request.ReleaseId is not null) incident.ReleaseId = RepairPolicy.Redact(request.ReleaseId);
        RepairStore.Event(incident, request.Detail, "node-3");
        if (request.Status == "needs_evidence")
        { incident.ProposalHash = ""; incident.ApprovedHash = ""; }
        if (request.Status is "failed" or "completed" or "rolled_back" or "awaiting_approval" or "ready" or "needs_evidence")
        { incident.LeaseToken = null; incident.LeaseUntil = null; }
        if (request.Status == "failed" && previousStatus is "diagnosing" or "repairing" or "testing" && incident.Attempts < 3)
        {
            incident.Status = "queued";
            incident.ProposalHash = "";
            incident.ApprovedHash = "";
            RepairStore.Event(incident, "ستتم إعادة التشخيص ضمن حد ثلاث محاولات", "supervisor");
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { incident.Status });
    }

    public sealed record LeaseRequest(Guid LeaseToken);
    public sealed record ReportRequest(Guid LeaseToken, string Status, string Detail, string? ProposalHash, string? ReleaseId);
}
