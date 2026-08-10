using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Services;

public static class RechargeMatchRules
{
    public const int WindowMinutes = 120;

    public static DateTime Anchor(RechargeRequest request) => request.UpdatedAt ?? request.CreatedAt;

    public static DateTime WindowStart(DateTime anchor) => anchor.AddMinutes(-WindowMinutes);

    public static DateTime WindowEnd(DateTime anchor) => anchor.AddMinutes(WindowMinutes);

    public static bool IsWithinWindow(DateTime timestamp, DateTime anchor) =>
        timestamp >= WindowStart(anchor) && timestamp <= WindowEnd(anchor);
}
