using Microsoft.EntityFrameworkCore;
using Npgsql;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignService
{
    public async Task<bool> ProcessReceiptAsync(
        string metaMessageId,
        string? status,
        DateTime providerTimestamp,
        string? failureCode,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ProcessReceiptOnceAsync(
                    metaMessageId, status, providerTimestamp, failureCode, ct);
            }
            catch (Exception exception) when (attempt < 3 && IsProjectionConcurrencyFailure(exception))
            {
                _db.ClearTrackedChanges();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
            }
        }
    }

    private async Task<bool> ProcessReceiptOnceAsync(
        string metaMessageId,
        string? status,
        DateTime providerTimestamp,
        string? failureCode,
        CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var recipient = await _db.WhatsAppCampaignRecipients
            .SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        if (recipient is null)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
        var pending = await _db.LiveSupportWhatsAppPendingReceipts
            .SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        var changed = pending is not null && ApplyCampaignReceipt(
            recipient, pending.Status, pending.ProviderTimestamp, pending.FailureCode,
            pending.DeliveredAt, pending.ReadAt);
        if (pending is not null) _db.LiveSupportWhatsAppPendingReceipts.Remove(pending);
        changed = ApplyCampaignReceipt(recipient, status, providerTimestamp, failureCode) || changed;
        if (changed || pending is not null)
        {
            await _db.SaveChangesAsync(ct);
            var campaign = await _db.WhatsAppCampaigns.SingleAsync(item => item.Id == recipient.CampaignId, ct);
            await RefreshCountersAsync(campaign, ct);
            await CompleteCampaignIfTerminalAsync(campaign, ct);
            await _db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> ReconcilePendingReceiptAsync(string metaMessageId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ReconcilePendingReceiptOnceAsync(metaMessageId, ct);
            }
            catch (Exception exception) when (attempt < 3 && IsProjectionConcurrencyFailure(exception))
            {
                _db.ClearTrackedChanges();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
            }
        }
    }

    private async Task<bool> ReconcilePendingReceiptOnceAsync(string metaMessageId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(metaMessageId)) return false;
        await using var transaction = await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var pending = await _db.LiveSupportWhatsAppPendingReceipts
            .SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        if (pending is null)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
        var recipient = await _db.WhatsAppCampaignRecipients
            .SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        if (recipient is null)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
        var changed = ApplyCampaignReceipt(recipient, pending.Status, pending.ProviderTimestamp,
            pending.FailureCode, pending.DeliveredAt, pending.ReadAt);
        _db.LiveSupportWhatsAppPendingReceipts.Remove(pending);
        await _db.SaveChangesAsync(ct);
        if (changed)
        {
            var campaign = await _db.WhatsAppCampaigns.SingleAsync(item => item.Id == recipient.CampaignId, ct);
            await RefreshCountersAsync(campaign, ct);
            await CompleteCampaignIfTerminalAsync(campaign, ct);
        }
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    internal Task CompleteCampaignIfTerminalAsync(WhatsAppCampaign campaign, CancellationToken ct) =>
        CompleteCampaignsIfTerminalAsync([campaign], ct);

    internal async Task CompleteCampaignsIfTerminalAsync(
        IReadOnlyList<WhatsAppCampaign> campaigns,
        CancellationToken ct)
    {
        var candidates = campaigns.Where(campaign => campaign.Status is
            WhatsAppCampaignStatus.Running or WhatsAppCampaignStatus.Paused).ToArray();
        if (candidates.Length == 0) return;
        var campaignIds = candidates.Select(campaign => campaign.Id).ToArray();
        var unfinishedCampaignIds = await _db.WhatsAppCampaignRecipients.AsNoTracking().Where(item =>
            campaignIds.Contains(item.CampaignId) &&
            (item.Status == WhatsAppCampaignRecipientStatus.Pending ||
             item.Status == WhatsAppCampaignRecipientStatus.Sending))
            .Select(item => item.CampaignId)
            .Distinct()
            .ToArrayAsync(ct);
        var unfinished = unfinishedCampaignIds.ToHashSet();
        foreach (var campaign in candidates.Where(campaign => !unfinished.Contains(campaign.Id)))
        {
            campaign.Status = WhatsAppCampaignStatus.Completed;
            campaign.CompletedAt = DateTime.UtcNow;
            campaign.UpdatedAt = campaign.CompletedAt;
            campaign.Version++;
        }
    }

    internal static bool ApplyCampaignReceipt(
        WhatsAppCampaignRecipient recipient,
        string? incomingStatus,
        DateTime providerTimestamp,
        string? failureCode,
        DateTime? observedDeliveredAt = null,
        DateTime? observedReadAt = null)
    {
        var before = (recipient.Status, recipient.ProviderTimestamp, recipient.DeliveredAt,
            recipient.ReadAt, recipient.FailureCode);
        switch (incomingStatus?.Trim().ToLowerInvariant())
        {
            case "sent":
                if (IsOlderTerminalObservation(recipient, providerTimestamp) ||
                    recipient.ProviderTimestamp == providerTimestamp &&
                    recipient.Status == WhatsAppCampaignRecipientStatus.Failed)
                    break;
                if (CampaignSuccessRank(recipient.Status) > CampaignSuccessRank(WhatsAppCampaignRecipientStatus.Sent))
                    break;
                recipient.Status = WhatsAppCampaignRecipientStatus.Sent;
                recipient.SentAt ??= providerTimestamp;
                recipient.FailureCode = null;
                recipient.ProviderTimestamp = Later(recipient.ProviderTimestamp, providerTimestamp);
                break;
            case "delivered":
                if (CampaignSuccessRank(recipient.Status) < CampaignSuccessRank(WhatsAppCampaignRecipientStatus.Delivered))
                    recipient.Status = WhatsAppCampaignRecipientStatus.Delivered;
                recipient.SentAt ??= providerTimestamp;
                recipient.DeliveredAt = Earlier(recipient.DeliveredAt, observedDeliveredAt ?? providerTimestamp);
                recipient.FailureCode = null;
                recipient.ProviderTimestamp = Later(recipient.ProviderTimestamp, providerTimestamp);
                break;
            case "read":
                recipient.Status = WhatsAppCampaignRecipientStatus.Read;
                recipient.SentAt ??= providerTimestamp;
                recipient.DeliveredAt = Earlier(recipient.DeliveredAt, observedDeliveredAt ?? providerTimestamp);
                recipient.ReadAt = Earlier(recipient.ReadAt, observedReadAt ?? providerTimestamp);
                recipient.FailureCode = null;
                recipient.ProviderTimestamp = Later(recipient.ProviderTimestamp, providerTimestamp);
                break;
            case "failed":
                if (IsOlderTerminalObservation(recipient, providerTimestamp)) break;
                if (CampaignSuccessRank(recipient.Status) >= CampaignSuccessRank(WhatsAppCampaignRecipientStatus.Delivered))
                    break;
                recipient.Status = WhatsAppCampaignRecipientStatus.Failed;
                recipient.FailureCode = failureCode ?? "WHATSAPP_DELIVERY_FAILED";
                recipient.ProviderTimestamp = Later(recipient.ProviderTimestamp, providerTimestamp);
                break;
            default:
                return false;
        }
        if (observedDeliveredAt.HasValue)
            recipient.DeliveredAt = Earlier(recipient.DeliveredAt, observedDeliveredAt);
        if (observedReadAt.HasValue)
            recipient.ReadAt = Earlier(recipient.ReadAt, observedReadAt);
        var after = (recipient.Status, recipient.ProviderTimestamp, recipient.DeliveredAt,
            recipient.ReadAt, recipient.FailureCode);
        if (before == after) return false;
        recipient.UpdatedAt = DateTime.UtcNow;
        recipient.Version++;
        return true;
    }

    private static bool IsOlderTerminalObservation(
        WhatsAppCampaignRecipient recipient,
        DateTime providerTimestamp) =>
        recipient.Status is WhatsAppCampaignRecipientStatus.Sent or WhatsAppCampaignRecipientStatus.Failed &&
        recipient.ProviderTimestamp.HasValue &&
        providerTimestamp < recipient.ProviderTimestamp.Value;

    private static int CampaignSuccessRank(WhatsAppCampaignRecipientStatus status) => status switch
    {
        WhatsAppCampaignRecipientStatus.Sent => 1,
        WhatsAppCampaignRecipientStatus.Delivered => 2,
        WhatsAppCampaignRecipientStatus.Read => 3,
        _ => 0
    };

    private static DateTime Later(DateTime? current, DateTime candidate) =>
        !current.HasValue || candidate > current.Value ? candidate : current.Value;

    private static DateTime? Earlier(DateTime? current, DateTime? candidate)
    {
        if (!candidate.HasValue) return current;
        return !current.HasValue || candidate.Value < current.Value ? candidate : current;
    }

    private static bool IsProjectionConcurrencyFailure(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } => true,
        DbUpdateException
        {
            InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        } => true,
        _ => false
    };
}
