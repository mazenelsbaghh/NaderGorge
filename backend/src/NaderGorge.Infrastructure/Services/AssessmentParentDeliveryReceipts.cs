using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

internal static class AssessmentParentDeliveryReceipts
{
    public static async Task ReconcileAsync(IAppDbContext db, string messageId, CancellationToken ct)
    {
        if (!await db.AssessmentParentDeliveries.AnyAsync(item => item.MetaMessageId == messageId, ct)) return;
        var receipt = await db.LiveSupportWhatsAppPendingReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MetaMessageId == messageId, ct);
        if (receipt is null || receipt.Status is not ("Failed" or "Delivered" or "Read")) return;
        var failed = receipt.Status == "Failed";
        // Keep the accumulated receipt as evidence and reject a stale projection if another webhook advanced it.
        await db.AssessmentParentDeliveries.Where(item => item.MetaMessageId == messageId
                && (item.Status == AssessmentParentDeliveryStatus.Sent || item.Status == AssessmentParentDeliveryStatus.Failed
                    || item.Status == AssessmentParentDeliveryStatus.Uncertain)
                && db.LiveSupportWhatsAppPendingReceipts.Any(pending => pending.MetaMessageId == messageId
                    && pending.Version == receipt.Version && pending.Status == receipt.Status))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, failed ? AssessmentParentDeliveryStatus.Failed : AssessmentParentDeliveryStatus.Sent)
                .SetProperty(item => item.FailureCode, failed ? receipt.FailureCode ?? "WHATSAPP_DELIVERY_FAILED" : null), ct);
    }
}
