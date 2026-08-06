using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public static class RechargeRequestExpiryService
{
    public const int PendingLifetimeHours = 48;
    public const string ReservationExpiredReason = "انتهت مهلة حجز المحفظة قبل رفع إثبات التحويل.";
    public const string AutoRejectionReason = "تم رفض الطلب تلقائياً لانتهاء مهلة المراجعة بعد 48 ساعة.";

    public static async Task ResolveExpiredPendingRequests(IAppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var reviewCutoff = now.AddHours(-PendingLifetimeHours);
        var expiredRequests = await db.RechargeRequests
            .Where(request => request.Status == RechargeRequestStatus.Pending
                && (request.CreatedAt <= reviewCutoff
                    || (request.ReservationExpiresAt <= now
                        && (request.ScreenshotUrl == null || request.ScreenshotUrl == ""))))
            .ToListAsync(ct);

        if (expiredRequests.Count == 0)
            return;

        foreach (var request in expiredRequests)
        {
            var reservationExpired = request.ReservationExpiresAt <= now
                && string.IsNullOrWhiteSpace(request.ScreenshotUrl);
            request.Status = reservationExpired
                ? RechargeRequestStatus.Expired
                : RechargeRequestStatus.Rejected;
            request.ResolvedAt = now;
            request.RejectionReason = reservationExpired
                ? ReservationExpiredReason
                : AutoRejectionReason;
            request.ReservationExpiresAt = null;
        }

        await db.SaveChangesAsync(ct);
    }
}
