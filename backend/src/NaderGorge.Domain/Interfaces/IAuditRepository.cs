using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditLog log);
}
