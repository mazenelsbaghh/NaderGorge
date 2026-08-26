using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

/// <summary>
/// Claims and sends frozen campaign recipients. A cluster lease must wrap each batch;
/// atomic recipient claims remain the final duplicate-send fence.
/// </summary>
public sealed class WhatsAppCampaignDispatcher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WhatsAppCampaignDispatcher> logger)
{
    private const int MaximumAttempts = 5;
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(5);
    private readonly int _batchSize = Math.Clamp(
        configuration.GetValue("WhatsAppCampaigns:DispatchBatchSize", 10), 1, 100);
    private readonly int _messagesPerSecond = Math.Clamp(
        configuration.GetValue("WhatsAppCampaigns:MessagesPerSecond", 10), 1, 20);

    public async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now.Subtract(StaleClaimAge);
        Guid[] recipientIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            recipientIds = await db.WhatsAppCampaignRecipients.AsNoTracking()
                .Where(recipient =>
                    recipient.Status == WhatsAppCampaignRecipientStatus.Sending &&
                    recipient.ClaimedAt < staleBefore ||
                    recipient.Status == WhatsAppCampaignRecipientStatus.Pending &&
                    (recipient.NextAttemptAt == null || recipient.NextAttemptAt <= now) &&
                    db.WhatsAppCampaigns.Any(campaign => campaign.Id == recipient.CampaignId &&
                        campaign.Status == WhatsAppCampaignStatus.Running))
                .OrderBy(recipient => recipient.Status == WhatsAppCampaignRecipientStatus.Sending ? 0 : 1)
                .ThenBy(recipient => recipient.CreatedAt)
                .ThenBy(recipient => recipient.Id)
                .Select(recipient => recipient.Id)
                .Take(_batchSize)
                .ToArrayAsync(ct);
        }

        var processed = 0;
        var interval = TimeSpan.FromMilliseconds(Math.Max(50, 1_000d / _messagesPerSecond));
        foreach (var recipientId in recipientIds)
        {
            try
            {
                await DispatchRecipientAsync(recipientId, staleBefore, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await MarkUncertainSafelyAsync(recipientId, "WHATSAPP_DISPATCH_CANCELLED", CancellationToken.None);
                throw;
            }
            catch (ProviderAcceptedPersistenceException exception)
            {
                logger.LogError(exception,
                    "Campaign recipient {RecipientId} was accepted by Meta but persistence failed.", recipientId);
                await MarkUncertainSafelyAsync(
                    recipientId, "WHATSAPP_PROVIDER_ACCEPTED_PERSISTENCE_FAILED", CancellationToken.None);
            }
            catch (Exception exception)
            {
                // Once a row is Sending, an unexpected exception is treated as ambiguous.
                // This intentionally trades a missed message for duplicate-send safety.
                logger.LogError(exception, "Campaign recipient {RecipientId} dispatch failed unexpectedly.", recipientId);
                await MarkUncertainSafelyAsync(recipientId, "WHATSAPP_DELIVERY_UNCERTAIN", CancellationToken.None);
            }
            processed++;
            if (processed < recipientIds.Length) await Task.Delay(interval, ct);
        }
        return processed;
    }

    private async Task DispatchRecipientAsync(Guid recipientId, DateTime staleBefore, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var campaigns = scope.ServiceProvider.GetRequiredService<WhatsAppCampaignService>();
        var cloud = scope.ServiceProvider.GetRequiredService<WhatsAppCloudService>();
        var protector = scope.ServiceProvider.GetRequiredService<IWhatsAppCampaignDataProtector>();
        var now = DateTime.UtcNow;

        var stale = await db.WhatsAppCampaignRecipients
            .Where(recipient => recipient.Id == recipientId &&
                recipient.Status == WhatsAppCampaignRecipientStatus.Sending &&
                recipient.ClaimedAt < staleBefore)
            .ExecuteUpdateAsync(update => update
                .SetProperty(recipient => recipient.Status, WhatsAppCampaignRecipientStatus.Uncertain)
                .SetProperty(recipient => recipient.FailureCode, "WHATSAPP_STALE_SENDING_UNCERTAIN")
                .SetProperty(recipient => recipient.ClaimedAt, (DateTime?)null)
                .SetProperty(recipient => recipient.UpdatedAt, now)
                .SetProperty(recipient => recipient.Version, recipient => recipient.Version + 1), ct);
        if (stale > 0)
        {
            await RefreshCampaignForRecipientAsync(db, campaigns, recipientId, ct);
            return;
        }

        var claimed = await db.WhatsAppCampaignRecipients
            .Where(recipient => recipient.Id == recipientId &&
                recipient.Status == WhatsAppCampaignRecipientStatus.Pending &&
                recipient.AttemptCount < MaximumAttempts &&
                (recipient.NextAttemptAt == null || recipient.NextAttemptAt <= now) &&
                db.WhatsAppCampaigns.Any(campaign => campaign.Id == recipient.CampaignId &&
                    campaign.Status == WhatsAppCampaignStatus.Running))
            .ExecuteUpdateAsync(update => update
                .SetProperty(recipient => recipient.Status, WhatsAppCampaignRecipientStatus.Sending)
                .SetProperty(recipient => recipient.AttemptCount, recipient => recipient.AttemptCount + 1)
                .SetProperty(recipient => recipient.ClaimedAt, now)
                .SetProperty(recipient => recipient.NextAttemptAt, (DateTime?)null)
                .SetProperty(recipient => recipient.FailureCode, (string?)null)
                .SetProperty(recipient => recipient.UpdatedAt, now)
                .SetProperty(recipient => recipient.Version, recipient => recipient.Version + 1), ct);
        if (claimed == 0)
        {
            await FinalizeExhaustedAsync(db, campaigns, recipientId, ct);
            return;
        }

        var recipient = await db.WhatsAppCampaignRecipients.SingleAsync(item => item.Id == recipientId, ct);
        var campaign = await db.WhatsAppCampaigns.SingleAsync(item => item.Id == recipient.CampaignId, ct);
        if (campaign.Status != WhatsAppCampaignStatus.Running)
        {
            SetStoppedCampaignOutcome(recipient, campaign.Status);
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }

        var template = await db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaign.TemplateId, ct);
        if (!CurrentTemplateMatches(template, campaign))
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Pending;
            recipient.ClaimedAt = null;
            recipient.FailureCode = null;
            recipient.UpdatedAt = DateTime.UtcNow;
            recipient.Version++;
            PauseForTemplateDrift(campaign);
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }

        if (!await campaigns.CurrentRecipientDestinationMatchesAsync(recipient, ct))
        {
            SetTerminal(recipient, WhatsAppCampaignRecipientStatus.Skipped,
                "WHATSAPP_DESTINATION_CHANGED_OR_INACTIVE");
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }
        if (!await campaigns.IsDestinationConsentedAsync(
                recipient.DestinationHash, campaign.TemplateCategory, ct))
        {
            SetTerminal(recipient, WhatsAppCampaignRecipientStatus.Skipped,
                "WHATSAPP_CONTACT_OPTED_OUT");
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }

        WhatsAppCampaignService.FrozenRecipientPayload payload;
        try
        {
            var plaintext = protector.Unprotect(
                recipient.Id, recipient.ProtectedPayload, recipient.PayloadDigest);
            payload = WhatsAppCampaignService.DeserializeFrozenRecipientPayload(plaintext)
                ?? throw new JsonException("Campaign payload is empty.");
            var normalized = WhatsAppCampaignService.NormalizeE164(payload.Destination);
            if (normalized is null || payload.Components is null ||
                !string.Equals(protector.DestinationHash(normalized), recipient.DestinationHash,
                    StringComparison.Ordinal))
                throw new JsonException("Campaign payload destination does not match its snapshot.");
        }
        catch (Exception exception) when (exception is JsonException or System.Security.Cryptography.CryptographicException)
        {
            SetTerminal(recipient, WhatsAppCampaignRecipientStatus.Failed,
                "WHATSAPP_CAMPAIGN_PAYLOAD_INVALID");
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }

        // Final kill switch immediately before the provider call. This re-reads all
        // mutable authority after claim and after decrypting the immutable snapshot.
        var currentCampaignStatus = await db.WhatsAppCampaigns.AsNoTracking()
            .Where(item => item.Id == campaign.Id)
            .Select(item => (WhatsAppCampaignStatus?)item.Status)
            .SingleOrDefaultAsync(ct);
        var templateStillCurrent = await db.LiveSupportWhatsAppTemplates.AsNoTracking().AnyAsync(item =>
            item.Id == campaign.TemplateId && item.Status == "APPROVED" &&
            item.Fingerprint == campaign.TemplateFingerprint, ct);
        if (currentCampaignStatus != WhatsAppCampaignStatus.Running)
        {
            await db.Entry(campaign).ReloadAsync(ct);
            SetStoppedCampaignOutcome(recipient, currentCampaignStatus ?? WhatsAppCampaignStatus.Failed);
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }
        if (!templateStillCurrent)
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Pending;
            recipient.ClaimedAt = null;
            recipient.UpdatedAt = DateTime.UtcNow;
            recipient.Version++;
            PauseForTemplateDrift(campaign);
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }
        if (!await campaigns.CurrentRecipientDestinationMatchesAsync(recipient, ct) ||
            !await campaigns.IsDestinationConsentedAsync(
                recipient.DestinationHash, campaign.TemplateCategory, ct))
        {
            SetTerminal(recipient, WhatsAppCampaignRecipientStatus.Skipped,
                "WHATSAPP_PRE_SEND_AUTHORITY_CHANGED");
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
            return;
        }

        WhatsAppCloudService.SendTestMessageResult response;
        try
        {
            response = await cloud.SendTemplateAsync(new WhatsAppCloudService.TemplateMessageRequest(
                payload.Destination,
                campaign.TemplateName,
                campaign.TemplateLanguage,
                payload.Components), ct);
        }
        catch (OperationCanceledException)
        {
            SetTerminal(recipient, WhatsAppCampaignRecipientStatus.Uncertain,
                "WHATSAPP_DELIVERY_UNCERTAIN");
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, CancellationToken.None);
            throw;
        }

        ApplyProviderOutcome(recipient, response);
        try
        {
            await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
        }
        catch (Exception exception) when (response.Success && exception is not OperationCanceledException)
        {
            throw new ProviderAcceptedPersistenceException(exception);
        }

        if (response.Success && !string.IsNullOrWhiteSpace(response.MetaMessageId))
            await campaigns.ReconcilePendingReceiptAsync(response.MetaMessageId, ct);
    }

    private static bool CurrentTemplateMatches(
        LiveSupportWhatsAppTemplate? template,
        WhatsAppCampaign campaign)
    {
        if (template is null ||
            !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(template.Fingerprint, campaign.TemplateFingerprint, StringComparison.Ordinal))
            return false;
        try
        {
            WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template);
            return true;
        }
        catch (NaderGorge.Application.Features.LiveSupport.Dtos.WhatsAppCampaignException)
        {
            return false;
        }
    }

    private static void PauseForTemplateDrift(WhatsAppCampaign campaign)
    {
        if (campaign.Status != WhatsAppCampaignStatus.Running) return;
        campaign.Status = WhatsAppCampaignStatus.Paused;
        campaign.PausedAt = DateTime.UtcNow;
        campaign.PauseReason = "تغير قالب واتساب بعد المراجعة؛ راجع الحملة قبل الاستكمال.";
        campaign.UpdatedAt = campaign.PausedAt;
        campaign.Version++;
    }

    private static void ApplyProviderOutcome(
        WhatsAppCampaignRecipient recipient,
        WhatsAppCloudService.SendTestMessageResult response)
    {
        if (response.Success && !string.IsNullOrWhiteSpace(response.MetaMessageId))
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Sent;
            recipient.MetaMessageId = response.MetaMessageId;
            recipient.SentAt ??= DateTime.UtcNow;
            recipient.FailureCode = null;
            recipient.NextAttemptAt = null;
        }
        else if (IsAmbiguous(response))
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Uncertain;
            recipient.FailureCode = "WHATSAPP_DELIVERY_UNCERTAIN";
            recipient.NextAttemptAt = null;
        }
        else if (response.IsRetryable && recipient.AttemptCount < MaximumAttempts)
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Pending;
            recipient.FailureCode = BoundedFailureCode(response.ErrorCode);
            recipient.NextAttemptAt = DateTime.UtcNow.Add(RetryDelay(recipient.AttemptCount, response.StatusCode));
        }
        else
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Failed;
            recipient.FailureCode = BoundedFailureCode(response.ErrorCode);
            recipient.NextAttemptAt = null;
        }
        recipient.ClaimedAt = null;
        recipient.UpdatedAt = DateTime.UtcNow;
        recipient.Version++;
    }

    internal static bool IsAmbiguous(WhatsAppCloudService.SendTestMessageResult response)
    {
        if (string.Equals(response.ErrorCode, "WHATSAPP_CLOUD_NOT_CONFIGURED", StringComparison.Ordinal))
            return false;
        return response.StatusCode == 408 || response.StatusCode >= 500 ||
            string.Equals(response.ErrorCode, "WHATSAPP_CLOUD_REQUEST_FAILED", StringComparison.Ordinal) ||
            string.Equals(response.ErrorCode, "WHATSAPP_CLOUD_INVALID_RESPONSE", StringComparison.Ordinal);
    }

    private static TimeSpan RetryDelay(int attempt, int statusCode)
    {
        if (statusCode == 429) return TimeSpan.FromMinutes(2);
        return TimeSpan.FromSeconds(Math.Min(300, 10 * Math.Pow(2, Math.Clamp(attempt - 1, 0, 5))));
    }

    private static string BoundedFailureCode(string? errorCode)
    {
        var value = string.IsNullOrWhiteSpace(errorCode) ? "WHATSAPP_DELIVERY_FAILED" : errorCode.Trim();
        var safe = new string(value.Take(120)
            .Select(character => char.IsAsciiLetterOrDigit(character) || character == '_'
                ? character
                : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "WHATSAPP_DELIVERY_FAILED" : safe;
    }

    private static void SetStoppedCampaignOutcome(
        WhatsAppCampaignRecipient recipient,
        WhatsAppCampaignStatus campaignStatus)
    {
        if (campaignStatus == WhatsAppCampaignStatus.Paused)
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Pending;
            recipient.FailureCode = null;
        }
        else
        {
            recipient.Status = WhatsAppCampaignRecipientStatus.Skipped;
            recipient.FailureCode = "WHATSAPP_CAMPAIGN_NOT_RUNNING";
        }
        recipient.ClaimedAt = null;
        recipient.UpdatedAt = DateTime.UtcNow;
        recipient.Version++;
    }

    private static void SetTerminal(
        WhatsAppCampaignRecipient recipient,
        WhatsAppCampaignRecipientStatus status,
        string failureCode)
    {
        recipient.Status = status;
        recipient.FailureCode = failureCode;
        recipient.NextAttemptAt = null;
        recipient.ClaimedAt = null;
        recipient.UpdatedAt = DateTime.UtcNow;
        recipient.Version++;
    }

    private static async Task PersistRecipientAndProjectionAsync(
        IAppDbContext db,
        WhatsAppCampaignService campaigns,
        WhatsAppCampaign campaign,
        CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.SaveChangesAsync(ct);
        await campaigns.RefreshCountersAsync(campaign, ct);
        await campaigns.CompleteCampaignIfTerminalAsync(campaign, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task RefreshCampaignForRecipientAsync(
        IAppDbContext db,
        WhatsAppCampaignService campaigns,
        Guid recipientId,
        CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var campaignId = await db.WhatsAppCampaignRecipients.AsNoTracking()
            .Where(item => item.Id == recipientId)
            .Select(item => item.CampaignId)
            .SingleOrDefaultAsync(ct);
        if (campaignId == Guid.Empty)
        {
            await transaction.RollbackAsync(ct);
            return;
        }
        var campaign = await db.WhatsAppCampaigns.SingleAsync(item => item.Id == campaignId, ct);
        await campaigns.RefreshCountersAsync(campaign, ct);
        await campaigns.CompleteCampaignIfTerminalAsync(campaign, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task FinalizeExhaustedAsync(
        IAppDbContext db,
        WhatsAppCampaignService campaigns,
        Guid recipientId,
        CancellationToken ct)
    {
        var recipient = await db.WhatsAppCampaignRecipients
            .SingleOrDefaultAsync(item => item.Id == recipientId &&
                item.Status == WhatsAppCampaignRecipientStatus.Pending &&
                item.AttemptCount >= MaximumAttempts, ct);
        if (recipient is null) return;
        var campaign = await db.WhatsAppCampaigns.SingleAsync(item => item.Id == recipient.CampaignId, ct);
        SetTerminal(recipient, WhatsAppCampaignRecipientStatus.Failed, "WHATSAPP_RETRY_EXHAUSTED");
        await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
    }

    private async Task MarkUncertainSafelyAsync(Guid recipientId, string failureCode, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var campaigns = scope.ServiceProvider.GetRequiredService<WhatsAppCampaignService>();
                var recipient = await db.WhatsAppCampaignRecipients.SingleOrDefaultAsync(
                    item => item.Id == recipientId, ct);
                if (recipient is null || recipient.Status is not (
                        WhatsAppCampaignRecipientStatus.Sending or
                        WhatsAppCampaignRecipientStatus.Uncertain))
                    return;
                var campaign = await db.WhatsAppCampaigns.SingleAsync(
                    item => item.Id == recipient.CampaignId, ct);
                if (recipient.Status == WhatsAppCampaignRecipientStatus.Sending)
                    SetTerminal(recipient, WhatsAppCampaignRecipientStatus.Uncertain, failureCode);
                await PersistRecipientAndProjectionAsync(db, campaigns, campaign, ct);
                return;
            }
            catch (Exception exception) when (attempt < 3 && IsProjectionConflict(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception,
                    "Could not persist uncertain state for campaign recipient {RecipientId}.", recipientId);
                return;
            }
        }
    }

    private static bool IsProjectionConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.SerializationFailure or
                Npgsql.PostgresErrorCodes.DeadlockDetected
        } => true,
        DbUpdateException
        {
            InnerException: Npgsql.PostgresException
            {
                SqlState: Npgsql.PostgresErrorCodes.SerializationFailure or
                    Npgsql.PostgresErrorCodes.DeadlockDetected
            }
        } => true,
        _ => false
    };

    private sealed class ProviderAcceptedPersistenceException(Exception innerException)
        : Exception("Provider accepted campaign delivery but persistence failed.", innerException);
}
