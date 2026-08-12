using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Queries;

public sealed class AdminAIAuditQueries(IAppDbContext db, IAdminAIAccessGate access)
{
    public async Task<AdminAIAuditEvidencePage> ListAsync(Guid requestingAdminId, string? cursor, int pageSize, string? capabilityKey, Guid? actorAdminUserId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(requestingAdminId, null, ct);
        if (pageSize is < 1 or > 100 || from > to || capabilityKey?.Length > 160) throw new ArgumentException("Invalid audit evidence filter.");
        var query = db.AdminAIAuditEvents.AsNoTracking().AsQueryable();
        if (capabilityKey is not null) query = query.Where(x => x.CapabilityKey == capabilityKey);
        if (actorAdminUserId is not null) query = query.Where(x => x.ActorAdminUserId == actorAdminUserId);
        if (from is not null) query = query.Where(x => x.OccurredAt >= from);
        if (to is not null) query = query.Where(x => x.OccurredAt <= to);
        if (cursor is not null) { var boundary = Decode(cursor); query = query.Where(x => x.OccurredAt < boundary.At || x.OccurredAt == boundary.At && x.Id.CompareTo(boundary.Id) < 0); }
        var rows = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(pageSize + 1).ToListAsync(ct);
        var visible = rows.Take(pageSize).Select(x => new AdminAIAuditEvidenceDto(x.Id, x.EventType.ToString(), x.ActorAdminUserId, x.ProposalId, x.ExecutionId, x.CapabilityKey, x.SafeEvidenceJson, x.EvidenceHash, x.CorrelationId, x.OccurredAt)).ToArray();
        return new(visible, rows.Count > pageSize ? Encode(rows[pageSize - 1].OccurredAt, rows[pageSize - 1].Id) : null);
    }

    private static string Encode(DateTime at, Guid id) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{at.Ticks}:{id:N}"));
    private static (DateTime At, Guid Id) Decode(string cursor)
    {
        try { var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':'); if (parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParseExact(parts[1], "N", out var id)) return (new DateTime(ticks, DateTimeKind.Utc), id); }
        catch (FormatException) { }
        throw new ArgumentException("Invalid audit cursor.", nameof(cursor));
    }
}
