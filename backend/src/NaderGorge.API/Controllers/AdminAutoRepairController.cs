using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.AutoRepair;
using NaderGorge.Application.Common;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/auto-repair")]
[Authorize(Roles = "Admin")]
public sealed class AdminAutoRepairController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var query = db.AutoRepairIncidents.AsNoTracking();
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
        var incidents = await query.OrderByDescending(x => x.LastSeen).Skip((Math.Clamp(page, 1, 10_000) - 1) * 30).Take(30)
            .Select(x => new { x.Id, x.Source, x.Category, x.Level, x.Status, x.Occurrences, x.Attempts, x.FirstSeen, x.LastSeen, x.Summary, x.ReleaseId }).ToArrayAsync(ct);
        var counts = await db.AutoRepairIncidents.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var control = await db.AutoRepairControls.AsNoTracking().SingleAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { control, incidents, counts, total = await query.CountAsync(ct),
            synchronization = await RepairSynchronization.Latest(db, ct),
            lastSynchronized = await RepairSynchronization.Latest(db, ct, "ready") }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var incident = await db.AutoRepairIncidents.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Status, x.Evidence, x.Summary, x.ProposalHash, x.ApprovedHash, x.ReleaseId,
                Events = x.Events.OrderByDescending(e => e.Id).Take(200).Select(e => new { e.Id, e.Timestamp, e.Status, e.Detail, e.Actor }) }).SingleOrDefaultAsync(ct);
        return incident is null ? NotFound() : Ok(ApiResponse<object>.Ok(incident));
    }

    [HttpPut("control")]
    public async Task<IActionResult> Control(ControlRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1700914)", ct);
        var control = await db.AutoRepairControls.SingleAsync(ct);
        control.Paused = request.Paused;
        control.AutoDeploy = request.AutoDeploy;
        db.AutoRepairEvents.Add(new NaderGorge.Domain.Entities.AutoRepairEvent
        {
            Status = "control", Actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin",
            Detail = $"Paused={control.Paused}; AutoDeploy={control.AutoDeploy}"
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok(control));
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> Decision(Guid id, DecisionRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1700914)", ct);
        var incident = await db.AutoRepairIncidents.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null) return NotFound();
        if (request.Action == "approve" && incident.Status is "awaiting_approval" or "ready" && incident.ProposalHash.Length == 64
            && request.ProposalHash == incident.ProposalHash && request.Confirmation == $"اعتماد {incident.ProposalHash[..12]}")
        {
            incident.ApprovedHash = incident.ProposalHash;
            incident.Status = "ready";
        }
        else if (request.Action == "retry" && incident.Status is "failed" or "rolled_back")
        {
            incident.Status = "queued";
            incident.Attempts = 0;
            incident.ApprovedHash = "";
        }
        else return Conflict(new { message = "الحالة تغيرت أو القرار لا يطابق الإصلاح المعروض" });
        RepairStore.Event(incident, $"قرار المالك: {request.Action}", User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { incident.Status }));
    }

    public sealed record ControlRequest(bool Paused, bool AutoDeploy);
    public sealed record DecisionRequest(string Action, string? ProposalHash, string? Confirmation);
}
