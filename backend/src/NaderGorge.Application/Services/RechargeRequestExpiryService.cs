using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public static class RechargeRequestExpiryService
{
    public const int PendingLifetimeHours = 48;
    public const string AutoRejectionReason = "تم رفض الطلب تلقائياً لانتهاء مهلة المراجعة بعد 48 ساعة.";

    public static async Task RejectPendingOlderThan48Hours(IAppDbContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddHours(-PendingLifetimeHours);
        var expiredRequests = await db.RechargeRequests
            .Where(r => r.Status == RechargeRequestStatus.Pending && r.CreatedAt <= cutoff)
            .ToListAsync(ct);

        if (expiredRequests.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var request in expiredRequests)
        {
            request.Status = RechargeRequestStatus.Rejected;
            request.ResolvedAt = now;
            request.RejectionReason = AutoRejectionReason;
            request.ReservationExpiresAt = null;
        }

        await db.SaveChangesAsync(ct);
    }
}
