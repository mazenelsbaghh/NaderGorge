using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Common.HR;

public interface IHrAuditWriter
{
    Task WriteMutationAsync(
        string action,
        string entityType,
        Guid? entityId,
        object? before,
        object? after,
        string reason,
        CancellationToken ct,
        Guid? actorUserId = null,
        string? systemActor = null);
}

public sealed class HrAuditWriter : IHrAuditWriter
{
    private static readonly string[] SensitiveFragments =
    [
        "password", "salary", "amount", "phone", "token", "secret", "bank",
        "document", "attachment", "evidence", "casebody", "investigation"
    ];

    private readonly IAppDbContext _db;
    private readonly IHrRequestContext _requestContext;

    public HrAuditWriter(IAppDbContext db, IHrRequestContext requestContext)
    {
        _db = db;
        _requestContext = requestContext;
    }

    public async Task WriteMutationAsync(
        string action,
        string entityType,
        Guid? entityId,
        object? before,
        object? after,
        string reason,
        CancellationToken ct,
        Guid? actorUserId = null,
        string? systemActor = null)
    {
        var resolvedActorId = actorUserId ?? _requestContext.ActorUserId;
        if (!resolvedActorId.HasValue && string.IsNullOrWhiteSpace(systemActor))
            throw new UnauthorizedAccessException("HR mutation requires an authenticated actor or a named system actor.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("HR mutation audit requires a reason.");

        string actorSnapshot;
        if (resolvedActorId.HasValue)
        {
            var actor = await _db.Users.AsNoTracking()
                .Where(item => item.Id == resolvedActorId.Value)
                .Select(item => new { item.Id, item.FullName, item.IsActive, item.SecurityStampVersion })
                .SingleOrDefaultAsync(ct)
                ?? throw new UnauthorizedAccessException("HR mutation actor does not exist.");
            actorSnapshot = JsonSerializer.Serialize(actor);
        }
        else
        {
            actorSnapshot = JsonSerializer.Serialize(new { service = systemActor!.Trim() });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PerformedByUserId = resolvedActorId,
            ActorType = resolvedActorId.HasValue ? "User" : "System",
            ActorSnapshot = actorSnapshot,
            OldValues = SerializeRedacted(before),
            NewValues = SerializeRedacted(after),
            Reason = reason.Trim(),
            IpAddress = _requestContext.IpAddress,
            RequestId = _requestContext.RequestId,
            CorrelationId = _requestContext.CorrelationId
        });
    }

    internal static string? SerializeRedacted(object? value)
    {
        if (value is null) return null;
        var node = JsonSerializer.SerializeToNode(value);
        Redact(node);
        return node?.ToJsonString();
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj.ToList())
            {
                if (SensitiveFragments.Any(fragment => pair.Key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                    obj[pair.Key] = "[REDACTED]";
                else
                    Redact(pair.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) Redact(item);
        }
    }
}

public sealed class DetachedHrRequestContext : IHrRequestContext
{
    public static DetachedHrRequestContext Instance { get; } = new();
    private DetachedHrRequestContext() { }
    public Guid? ActorUserId => null;
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
}
