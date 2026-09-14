using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Observability;
using StackExchange.Redis;

namespace NaderGorge.API.AutoRepair;

public sealed class RepairStore(AppDbContext db, IConnectionMultiplexer redis)
{
    // Called under the runner's advisory transaction lock. Preserve legacy rows
    // and their events, but prevent unattempted duplicates consuming model runs.
    public async Task ConsolidateQueued(CancellationToken ct)
    {
        var queued = await db.AutoRepairIncidents
            .Where(x => x.Status == "queued" && x.Attempts == 0 && x.LeaseToken == null)
            .OrderBy(x => x.FirstSeen).Take(500).ToListAsync(ct);
        foreach (var group in queued.GroupBy(x => RepairPolicy.Fingerprint(x.Source, x.Category, x.Evidence)))
        {
            var canonical = await db.AutoRepairIncidents.SingleOrDefaultAsync(x => x.Fingerprint == group.Key, ct)
                ?? group.First();
            canonical.Fingerprint = group.Key;
            foreach (var duplicate in group.Where(x => x.Id != canonical.Id))
            {
                canonical.Occurrences += duplicate.Occurrences;
                if (duplicate.FirstSeen < canonical.FirstSeen) canonical.FirstSeen = duplicate.FirstSeen;
                if (duplicate.LastSeen > canonical.LastSeen) canonical.LastSeen = duplicate.LastSeen;
                duplicate.Status = "duplicate";
                duplicate.Summary = $"تكرار محفوظ؛ المتابعة ضمن الحالة {canonical.Id}";
                Event(duplicate, duplicate.Summary, "collector");
                Event(canonical, $"جُمعت الحالة المكررة {duplicate.Id} مع الاحتفاظ بسجلها", "collector");
                if (canonical.Status == "completed")
                {
                    canonical.Status = "queued";
                    canonical.Attempts = 0;
                    canonical.ApprovedHash = "";
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task Ingest(CancellationToken ct)
    {
        var raw = await redis.GetDatabase().ListRangeAsync(RedisSystemLogProvider.RedisKey, -RedisSystemLogProvider.Capacity, -1);
        var logs = new List<RepairLog>();
        foreach (var entry in raw)
        {
            try
            {
                var log = JsonSerializer.Deserialize<RepairLog>(entry.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (log is not null && !string.IsNullOrEmpty(log.Message) && !string.IsNullOrEmpty(log.Category) && log.Source is "backend" or "worker" && log.Id != Guid.Empty && log.Timestamp <= DateTimeOffset.UtcNow && log.Level is "warning" or "error" or "critical") logs.Add(log);
            }
            catch (JsonException) { /* An invalid log must not block unrelated incidents. */ }
        }
        var ids = logs.Select(x => x.Id).ToArray();
        var seen = (await db.AutoRepairLogReceipts.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var control = await db.AutoRepairControls.SingleAsync(ct);
        foreach (var log in logs.OrderBy(x => x.Timestamp))
        {
            if (!seen.Add(log.Id) || log.Timestamp < control.LogCursor) continue;
            await AddLog(log, ct);
            db.AutoRepairLogReceipts.Add(new AutoRepairLogReceipt { Id = log.Id, Timestamp = log.Timestamp });
        }
        // Cursor moves behind the oldest retained log so the rolling Redis window can be reread safely.
        if (logs.Count > 0)
        {
            var oldest = logs.Min(x => x.Timestamp);
            if (!control.LogCursor.HasValue || oldest > control.LogCursor) control.LogCursor = oldest;
        }
        var receiptCutoff = DateTimeOffset.UtcNow.AddMinutes(-15);
        if (control.LogCursor.HasValue && control.LogCursor < receiptCutoff) receiptCutoff = control.LogCursor.Value;
        await db.AutoRepairLogReceipts.Where(x => x.Timestamp < receiptCutoff).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task IngestExternal(RepairLog[] logs, CancellationToken ct)
    {
        var valid = logs.Where(x => x.Id != Guid.Empty && x.Timestamp <= DateTimeOffset.UtcNow
            && x.Timestamp >= DateTimeOffset.UtcNow.AddMinutes(-10)
            && x.Source is "gateway" or "student" or "admin" or "teacher" or "staff" or "landing"
            && x.Level is "warning" or "error" or "critical"
            && !string.IsNullOrWhiteSpace(x.Message) && !string.IsNullOrWhiteSpace(x.Category)).ToArray();
        var ids = valid.Select(x => x.Id).ToArray();
        var seen = (await db.AutoRepairLogReceipts.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        foreach (var log in valid.OrderBy(x => x.Timestamp))
        {
            if (!seen.Add(log.Id)) continue;
            await AddLog(log, ct);
            db.AutoRepairLogReceipts.Add(new AutoRepairLogReceipt { Id = log.Id, Timestamp = log.Timestamp });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task AddLog(RepairLog log, CancellationToken ct)
    {
        var evidence = RepairPolicy.Redact($"{log.Message}\n{log.Exception}");
        var fingerprint = RepairPolicy.Fingerprint(log.Source, log.Category, evidence);
        var incident = db.AutoRepairIncidents.Local.FirstOrDefault(x => x.Fingerprint == fingerprint)
            ?? await db.AutoRepairIncidents.SingleOrDefaultAsync(x => x.Fingerprint == fingerprint, ct);
        if (incident is null)
        {
            incident = new AutoRepairIncident { Fingerprint = fingerprint, Source = RepairPolicy.Redact(log.Source), Category = RepairPolicy.Redact(log.Category), Level = log.Level, Evidence = evidence, FirstSeen = log.Timestamp };
            db.AutoRepairIncidents.Add(incident);
            Event(incident, "رُصدت مشكلة جديدة من سجل النظام", "collector");
        }
        incident.Occurrences++;
        incident.LastSeen = log.Timestamp > incident.LastSeen ? log.Timestamp : incident.LastSeen;
        if (incident.Status == "completed")
        {
            incident.Status = "queued";
            incident.Attempts = 0;
            incident.ApprovedHash = "";
            Event(incident, "ظهر الخطأ مجددًا بعد الإصلاح", "collector");
        }
    }

    public static void Event(AutoRepairIncident incident, string detail, string actor) =>
        incident.Events.Add(new AutoRepairEvent { IncidentId = incident.Id, Status = incident.Status, Detail = RepairPolicy.Redact(detail), Actor = actor });

    public sealed record RepairLog(Guid Id, DateTimeOffset Timestamp, string Source, string Category, string Level, string Message, string? Exception);
}
