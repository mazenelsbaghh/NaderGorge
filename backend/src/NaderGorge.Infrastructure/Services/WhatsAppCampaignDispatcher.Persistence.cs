using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignDispatcher
{
    internal async Task PersistProviderOutcomeAsync(
        Guid recipientId, WhatsAppCloudService.SendTestMessageResult response, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await PersistProviderOutcomeOnceAsync(recipientId, response, ct);
                return;
            }
            catch (Exception exception) when (attempt < 5 && LiveSupportWriteConflict.IsRetryable(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), ct);
            }
        }
    }

    private async Task PersistProviderOutcomeOnceAsync(
        Guid recipientId, WhatsAppCloudService.SendTestMessageResult response, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var campaigns = scope.ServiceProvider.GetRequiredService<WhatsAppCampaignService>();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var campaignId = await db.WhatsAppCampaignRecipients.AsNoTracking()
            .Where(item => item.Id == recipientId).Select(item => item.CampaignId).SingleAsync(ct);
        await campaigns.LockProjectionAsync(campaignId, ct);
        var recipient = await db.WhatsAppCampaignRecipients.SingleAsync(item => item.Id == recipientId, ct);
        if (recipient.Status is WhatsAppCampaignRecipientStatus.Sending or WhatsAppCampaignRecipientStatus.Uncertain)
            ApplyProviderOutcome(recipient, response);
        await db.SaveChangesAsync(ct);
        var campaign = await db.WhatsAppCampaigns.SingleAsync(item => item.Id == recipient.CampaignId, ct);
        await campaigns.RefreshCountersAsync(campaign, ct);
        await campaigns.CompleteCampaignIfTerminalAsync(campaign, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
