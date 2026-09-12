using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Infrastructure.Data;

internal static class LiveSupportEventSequences
{
    public static async Task AssignAsync(AppDbContext db, CancellationToken ct)
    {
        var pending = db.ChangeTracker.Entries<LiveSupportEvent>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity).ToArray();
        if (pending.Length == 0 || db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;

        var sequences = await db.Database.SqlQueryRaw<long>(
            "SELECT massar_next_support_event_sequence() AS \"Value\" FROM generate_series(1, {0})", pending.Length)
            .ToListAsync(ct);
        var assigned = new Dictionary<Guid, long>();
        for (var index = 0; index < pending.Length; index++)
        {
            pending[index].Sequence = sequences[index];
            assigned.Add(pending[index].Id, sequences[index]);
        }

        foreach (var entry in db.ChangeTracker.Entries<Domain.Entities.OutboxEvent>()
                     .Where(entry => entry.State == EntityState.Added && entry.Entity.Type == "LiveSupportEvent"))
        {
            var payload = JsonNode.Parse(entry.Entity.PayloadJson)!.AsObject();
            if (Guid.TryParse(payload["eventId"]?.GetValue<string>(), out var eventId)
                && assigned.TryGetValue(eventId, out var sequence))
            {
                payload["sequence"] = sequence;
                entry.Entity.PayloadJson = payload.ToJsonString();
            }
        }
    }
}
