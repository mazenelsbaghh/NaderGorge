using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Observability;
using StackExchange.Redis;

namespace NaderGorge.API.AutoRepair;

// Runs under the existing repair transaction lock. Never issues application requests or SQL supplied by a model.
public sealed class RepairEvidenceCollector(AppDbContext db, IConnectionMultiplexer redis)
{
    private const int MaximumRounds = 2;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(20);

    public async Task Collect(CancellationToken ct)
    {
        var control = await db.AutoRepairControls.SingleAsync(ct);
        if (control.Paused) return;
        var incidents = await db.AutoRepairIncidents
            .Where(x => x.LeaseToken == null && (x.Status == "collecting_evidence" || x.Status == "needs_evidence")
                && !x.Events.Any(e => e.Status == "collection_closed"))
            .Include(x => x.Events.Where(e => e.Status == "collecting_evidence"))
            .OrderBy(x => x.FirstSeen).Take(50).ToListAsync(ct);
        if (incidents.Count == 0) return;
        var logs = await RecentMeasurements();
        foreach (var incident in incidents) Advance(incident, logs, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<RepairStore.RepairLog[]> RecentMeasurements()
    {
        var entries = await redis.GetDatabase().ListRangeAsync(RedisSystemLogProvider.RedisKey, -1000, -1);
        var logs = new List<RepairStore.RepairLog>();
        foreach (var entry in entries)
        {
            try
            {
                var log = JsonSerializer.Deserialize<RepairStore.RepairLog>(entry.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (log is { Source: "backend", Category: "NaderGorge.API.Middleware.RequestPerformanceLoggingMiddleware" }
                    && log.Message is { Length: <= 8000 } && log.Message.Contains(" EvidenceV=1 ")) logs.Add(log);
            }
            catch (JsonException) { /* Invalid telemetry must not stop other evidence windows. */ }
        }
        return logs.ToArray();
    }

    internal static string? RouteKey(string evidence)
    {
        var match = Regex.Match(evidence, @"\bRoute=([^\s]{1,160}) Method=(GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)\b");
        return match.Success && match.Groups[1].Value != "unmatched" ? match.Value : null;
    }

    internal static void Advance(AutoRepairIncident incident, RepairStore.RepairLog[] logs, DateTimeOffset now)
    {
        var rounds = incident.Events.Where(e => e.Status == "collecting_evidence").OrderBy(e => e.Id).ToArray();
        var route = RouteKey(incident.Evidence);
        if (route is null)
        {
            Close(incident, "تعذّر تحديد مسار طلب لقياسه تلقائيًا. المطلوب: مسار الطلب أو خطوات إعادة الإنتاج؛ لم يُمنح المنفّذ وصولًا حرًا للإنتاج.");
            return;
        }
        if (incident.Status == "needs_evidence")
        {
            if (rounds.Length >= MaximumRounds)
            {
                Close(incident, "انتهت جولتا جمع القياسات دون إثبات إصلاح. راجع نتائج القياس والتشخيص وأضف خطوات إعادة إنتاج أو بيانات اختبار منقحة.");
                return;
            }
            incident.Status = "collecting_evidence";
            incident.Summary += "\nجارٍ جمع قياسات حديثة لنفس المسار لمدة أقصاها 20 دقيقة؛ المحاولة " + (rounds.Length + 1) + " من 2.";
            RepairStore.Event(incident, "بدأت نافذة جمع قياسات محدودة لنفس المسار؛ لا يتم تسجيل أجسام الطلبات أو SQL أو بيانات المستخدمين.", "collector");
            incident.Events.Last().Timestamp = now;
            return;
        }
        var started = rounds.LastOrDefault()?.Timestamp;
        if (started is null) { Close(incident, "سجل نافذة القياس مفقود؛ يلزم مراجعة الحالة قبل إعادة تشغيلها."); return; }
        var samples = logs.Where(x => x.Timestamp > started && x.Timestamp <= now
                && x.Timestamp <= started + Window && RouteKey(x.Message) == route)
            .OrderBy(x => x.Timestamp).DistinctBy(x => x.Id).Take(3).ToArray();
        if (samples.Length == 0)
        {
            if (now >= started + Window) Close(incident, "انتهت 20 دقيقة دون قياس جديد مطابق. المطلوب: إعادة تنفيذ المسار المتأثر بشكل طبيعي أو إضافة خطوات إعادة الإنتاج؛ لم نعتبر غياب اللوج إصلاحًا.");
            return;
        }
        var evidence = "قياسات طلبات جديدة لنفس المسار، وليست إعادة إنتاج مؤكدة للحادث القديم. زمن advisory_lock يشمل تنفيذ أمر القفل؛ ConnectionOpenMs يشمل فتح الاتصال ولا يفصل انتظار المسبح. الأوامر محدودة بأول 24 أمرًا.\n"
            + string.Join("\n", samples.Select(x => RepairPolicy.Redact(x.Message)));
        incident.Events.Add(new AutoRepairEvent { IncidentId = incident.Id, Status = "evidence", Actor = "collector", Detail = RepairPolicy.Redact(evidence), Timestamp = now });
        incident.Status = "queued";
        incident.Attempts = 0;
        incident.ProposalHash = "";
        incident.ApprovedHash = "";
        incident.Summary = "جُمعت قياسات جديدة تلقائيًا؛ في انتظار إعادة التشخيص. النتيجة السابقة محفوظة في السجل.";
        RepairStore.Event(incident, incident.Summary, "collector");
    }

    private static void Close(AutoRepairIncident incident, string reason)
    {
        incident.Status = "needs_evidence";
        incident.Summary += "\n" + reason;
        incident.Events.Add(new AutoRepairEvent { IncidentId = incident.Id, Status = "collection_closed", Actor = "collector", Detail = reason });
    }
}
