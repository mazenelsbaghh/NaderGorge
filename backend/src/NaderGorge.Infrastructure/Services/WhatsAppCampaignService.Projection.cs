using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignService
{
    internal async Task LockProjectionAsync(Guid campaignId, CancellationToken ct)
    {
        if (_db is not DbContext relational || !relational.Database.IsNpgsql()) return;
        // Receipt writers and the sender share this lock so counter snapshots cannot overwrite each other.
        await relational.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM whatsapp_campaigns WHERE \"Id\" = {campaignId} FOR UPDATE", ct);
    }
}
