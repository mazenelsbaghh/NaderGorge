using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIIdentitySummary(
    int ActiveUsers,
    int Students,
    int Staff,
    int Admins,
    int Roles,
    int Devices,
    int AccessGrants,
    int StudentBalances,
    int GamificationProfiles,
    int WatchEvents,
    DateTime DataAsOf);

public sealed class AdminAIIdentitySummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "identity.users.summary";
    public Type OutputType => typeof(AdminAIIdentitySummary);
    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await db.Users.AsNoTracking().CountAsync(x => x.IsActive && !x.IsDeleted, ct);
        var students = await db.UserRoles.AsNoTracking().CountAsync(x => x.Role.Type == RoleType.Student && x.User.IsActive && !x.User.IsDeleted, ct);
        var admins = await db.UserRoles.AsNoTracking().CountAsync(x => x.Role.Type == RoleType.Admin && x.User.IsActive && !x.User.IsDeleted, ct);
        var staff = await db.EmployeeProfiles.AsNoTracking().CountAsync(ct);
        var summary = new AdminAIIdentitySummary(
            active,
            students,
            staff,
            admins,
            await db.Roles.AsNoTracking().CountAsync(ct),
            await db.Devices.AsNoTracking().CountAsync(ct),
            await db.StudentAccessGrants.AsNoTracking().CountAsync(ct),
            await db.StudentBalances.AsNoTracking().CountAsync(ct),
            await db.StudentGamifications.AsNoTracking().CountAsync(ct),
            await db.VideoWatchEvents.AsNoTracking().CountAsync(ct),
            now);
        return new(summary, 1, true, false, now, ["admin.users"]);
    }
}
