using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.API.AutoRepair;

public static class RepairSynchronization
{
    public sealed record Node(string NodeId, string ReleaseId);
    public sealed record Snapshot(string State, string SharedCommit, Node[] Nodes);
    public sealed record Observation(DateTimeOffset CheckedAt, Snapshot Snapshot);

    public static bool Valid(Snapshot value)
    {
        if (value.State is not ("ready" or "pending_release" or "dependencies_changed" or "unavailable" or "storage_low" or "release_failed")) return false;
        if (value.SharedCommit.Length != 0 && !Regex.IsMatch(value.SharedCommit, "^[a-f0-9]{40}$")) return false;
        if (value.Nodes.Length > 3 || value.Nodes.Select(x => x.NodeId).Distinct().Count() != value.Nodes.Length) return false;
        if (value.Nodes.Any(x => x.NodeId is not ("node-1" or "node-2" or "node-3") || !Regex.IsMatch(x.ReleaseId, "^(git|src)-[a-f0-9]{7,40}$"))) return false;
        if (value.State == "ready" && value.Nodes.Select(x => x.ReleaseId).Distinct().Count() != 1) return false;
        return value.State is "unavailable" or "storage_low" || (value.SharedCommit.Length == 40 && value.Nodes.Length == 3);
    }

    public static async Task<Observation?> Latest(AppDbContext db, CancellationToken ct, string? state = null)
    {
        var query = db.AutoRepairEvents.AsNoTracking().Where(x => x.IncidentId == null && x.Actor == "synchronization");
        if (state is not null) query = query.Where(x => x.Status == state);
        var item = await query.OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return item is null ? null : new(item.Timestamp, JsonSerializer.Deserialize<Snapshot>(item.Detail)!);
    }

    public static bool AllowsClaim(Observation? value) => value is not null
        && value.Snapshot.State == "ready" && value.CheckedAt >= DateTimeOffset.UtcNow.AddMinutes(-3);

    public static async Task Record(AppDbContext db, Snapshot snapshot, CancellationToken ct)
    {
        var latest = await Latest(db, ct);
        var detail = JsonSerializer.Serialize(snapshot);
        // Retain durable transitions and at most one unchanged observation per minute.
        if (latest is null || latest.CheckedAt < DateTimeOffset.UtcNow.AddMinutes(-1)
            || JsonSerializer.Serialize(latest.Snapshot) != detail)
            db.AutoRepairEvents.Add(new AutoRepairEvent { Actor = "synchronization", Status = snapshot.State, Detail = detail });
        var control = await db.AutoRepairControls.SingleAsync(ct);
        control.Heartbeat = DateTimeOffset.UtcNow;
        control.Runner = "node-3";
        await db.SaveChangesAsync(ct);
    }
}
