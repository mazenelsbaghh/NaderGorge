using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Common;

public static class LiveSupportRoutingPermissions
{
    public const string ReceiveConversations = "live_support.route";
}

public sealed record LiveSupportRoutingEligibilityChange(Guid UserId, bool IsEligible, Guid ActorUserId);

public static class LiveSupportRoutingPermissionSync
{
    public static async Task SetEligibilityAsync(
        IAppDbContext db,
        LiveSupportRoutingEligibilityChange change,
        CancellationToken ct)
    {
        if (!await db.EmployeeProfiles.AnyAsync(profile => profile.UserId == change.UserId, ct)) return;

        var config = await db.LiveSupportStaffConfigs.FirstOrDefaultAsync(candidate => candidate.UserId == change.UserId, ct);
        if (config is null)
        {
            if (!change.IsEligible) return;
            db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig
            {
                UserId = change.UserId,
                IsEnabled = true,
                MaxActiveConversations = 1,
                ConfiguredByUserId = change.ActorUserId,
                Version = 1
            });
            return;
        }

        config.IsEnabled = change.IsEligible;
        config.ConfiguredByUserId = change.ActorUserId;
        config.Version++;
        config.UpdatedAt = DateTime.UtcNow;
    }
}
