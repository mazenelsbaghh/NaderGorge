using System.Text.Json;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, Guid? entityId, Guid? userId, object? oldValues = null, object? newValues = null, string? ipAddress = null, string? correlationId = null);
}

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repo;

    public AuditService(IAuditRepository repo)
    {
        _repo = repo;
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId, Guid? userId, object? oldValues = null, object? newValues = null, string? ipAddress = null, string? correlationId = null)
    {
        var log = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PerformedByUserId = userId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        };

        await _repo.AddAsync(log);
    }
}
