using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIAccessGate(IAppDbContext db) : IAdminAIAccessGate
{
    public async Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(
        Guid userId,
        int? expectedSecurityVersion,
        CancellationToken cancellationToken)
    {
        var snapshot = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && !user.IsDeleted)
            .Select(user => new
            {
                user.Id,
                user.SecurityStampVersion,
                IsAdmin = user.UserRoles.Any(link => link.Role.Type == RoleType.Admin),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null || !snapshot.IsAdmin)
            throw new UnauthorizedAccessException("A current active Admin account is required.");

        if (expectedSecurityVersion.HasValue && expectedSecurityVersion.Value != snapshot.SecurityStampVersion)
            throw new UnauthorizedAccessException("The Admin authorization state changed. Re-authentication is required.");

        return new AdminAIAccessSnapshot(snapshot.Id, snapshot.SecurityStampVersion, DateTime.UtcNow);
    }
}
