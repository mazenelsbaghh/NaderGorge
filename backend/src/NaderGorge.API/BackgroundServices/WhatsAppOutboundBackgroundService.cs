using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.BackgroundServices;

public sealed class WhatsAppOutboundBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppOutboundBackgroundService> logger) : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan PendingReceiptCleanupInterval = TimeSpan.FromDays(1);
    private DateTime _nextPendingReceiptCleanupAt = DateTime.MinValue;

    private sealed record DispatchContext(
        IAppDbContext Db,
        WhatsAppCloudService Cloud,
        WhatsAppLiveSupportService WhatsAppSupport,
        ILiveSupportAttachmentStorage AttachmentStorage,
        IWhatsAppOutboundMediaNormalizer MediaNormalizer,
        ILiveSupportEventWriter EventWriter);

    private sealed class ProviderAcceptedPersistenceException(Exception innerException)
        : Exception("A provider-accepted WhatsApp delivery could not be persisted.", innerException);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await CleanupPendingReceiptsIfDueAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupPendingReceiptsIfDueAsync(stoppingToken);
            try { await DispatchBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "WhatsApp outbound dispatch failed."); }
        }
    }

    internal async Task CleanupPendingReceiptsIfDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now < _nextPendingReceiptCleanupAt) return;
        _nextPendingReceiptCleanupAt = now.Add(PendingReceiptCleanupInterval);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<WhatsAppLiveSupportService>();
            await service.CleanupExpiredPendingReceiptsAsync(now, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "WhatsApp pending receipt cleanup failed.");
        }
    }

    internal async Task DispatchBatchAsync(CancellationToken ct)
    {
        var staleClaim = DateTime.UtcNow.AddMinutes(-5);
        List<Guid> candidates;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var now = DateTime.UtcNow;
            candidates = await db.LiveSupportWhatsAppMessages.AsNoTracking()
                .Where(message => message.Direction == "Outbound" &&
                    ((message.Status == "Pending" && (message.NextAttemptAt == null || message.NextAttemptAt <= now)) ||
                     (message.Status == "Sending" && message.ClaimedAt < staleClaim)) &&
                    !db.LiveSupportWhatsAppMessages.Any(previous =>
                        previous.ConversationId == message.ConversationId &&
                        previous.Direction == "Outbound" &&
                        (previous.Status == "Pending" || previous.Status == "Sending") &&
                        (previous.CreatedAt < message.CreatedAt ||
                         previous.CreatedAt == message.CreatedAt && previous.Id.CompareTo(message.Id) < 0)))
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Select(message => message.Id)
                .Take(20)
                .ToListAsync(ct);
        }

        foreach (var messageId in candidates)
        {
            try
            {
                await DispatchAsync(messageId, staleClaim, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (ProviderAcceptedPersistenceException exception)
            {
                logger.LogError(exception,
                    "WhatsApp outbound delivery {DeliveryId} was accepted by Meta but local persistence failed.",
                    messageId);
                await FinalizeUncertainDeliverySafelyAsync(messageId, ct);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "WhatsApp outbound delivery {DeliveryId} failed unexpectedly.", messageId);
                try
                {
                    await RecoverUnexpectedFailureAsync(messageId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception recoveryException)
                {
                    logger.LogError(recoveryException, "WhatsApp outbound delivery {DeliveryId} recovery failed.", messageId);
                }
            }
        }
    }

    private async Task DispatchAsync(
        Guid messageId,
        DateTime staleClaim,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = ResolveContext(scope);
        var claimedAt = DateTime.UtcNow;
        if (await FinalizeStaleSendingAsync(context, messageId, staleClaim, claimedAt, ct)) return;
        var claimed = await context.Db.LiveSupportWhatsAppMessages
            .Where(message => message.Id == messageId &&
                message.AttemptCount < MaxAttempts &&
                message.Status == "Pending" &&
                (message.NextAttemptAt == null || message.NextAttemptAt <= claimedAt) &&
                !context.Db.LiveSupportWhatsAppMessages.Any(previous =>
                    previous.ConversationId == message.ConversationId &&
                    previous.Direction == "Outbound" &&
                    (previous.Status == "Pending" || previous.Status == "Sending") &&
                    (previous.CreatedAt < message.CreatedAt ||
                     previous.CreatedAt == message.CreatedAt && previous.Id.CompareTo(message.Id) < 0)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(message => message.Status, "Sending")
                .SetProperty(message => message.ClaimedAt, claimedAt)
                .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1)
                .SetProperty(message => message.UpdatedAt, claimedAt)
                .SetProperty(message => message.Version, message => message.Version + 1), ct);
        if (claimed == 0)
        {
            await FinalizeExhaustedAsync(context, messageId, claimedAt, ct);
            return;
        }

        var delivery = await context.Db.LiveSupportWhatsAppMessages.SingleAsync(message => message.Id == messageId, ct);
        var binding = await context.Db.LiveSupportWhatsAppBindings.AsNoTracking()
            .SingleAsync(binding => binding.ConversationId == delivery.ConversationId, ct);
        var response = await SendAsync(context, delivery, binding.PhoneNumber, ct);
        CompleteAttempt(delivery, response);
        try
        {
            await context.Db.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (response.Success && exception is not OperationCanceledException)
        {
            throw new ProviderAcceptedPersistenceException(exception);
        }

        var reconciledPendingReceipt = response.Success &&
            !string.IsNullOrWhiteSpace(response.MetaMessageId) &&
            await context.WhatsAppSupport.ReconcilePendingReceiptAsync(response.MetaMessageId, ct);
        if (reconciledPendingReceipt) return;
        if (response.Success && await DeliveryAdvancedAfterProviderSaveAsync(context, delivery, ct)) return;

        await AppendDeliveryEventAsync(context, delivery, ct);
        await context.Db.SaveChangesAsync(ct);
    }

    private DispatchContext ResolveContext(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
        scope.ServiceProvider.GetRequiredService<WhatsAppCloudService>(),
        scope.ServiceProvider.GetRequiredService<WhatsAppLiveSupportService>(),
        scope.ServiceProvider.GetRequiredService<ILiveSupportAttachmentStorage>(),
        scope.ServiceProvider.GetRequiredService<IWhatsAppOutboundMediaNormalizer>(),
        scope.ServiceProvider.GetRequiredService<ILiveSupportEventWriter>());

    private static async Task<WhatsAppCloudService.SendTestMessageResult> SendAsync(
        DispatchContext context,
        LiveSupportWhatsAppMessage delivery,
        string phoneNumber,
        CancellationToken ct)
    {
        if (delivery.MessageType == "template") return await SendTemplateAsync(context, delivery, phoneNumber, ct);
        var supportMessage = await context.Db.LiveSupportMessages.AsNoTracking()
            .SingleAsync(message => message.Id == delivery.LiveSupportMessageId, ct);
        if (delivery.MessageType is "image" or "audio")
            return await SendMediaAsync(context, supportMessage, phoneNumber, ct);
        return await context.Cloud.SendTextAsync(phoneNumber, supportMessage.Content, ct);
    }

    private static async Task<WhatsAppCloudService.SendTestMessageResult> SendTemplateAsync(
        DispatchContext context,
        LiveSupportWhatsAppMessage delivery,
        string phoneNumber,
        CancellationToken ct)
    {
        var template = await context.Db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Name == delivery.TemplateName &&
                item.Language == delivery.TemplateLanguage && item.Status == "APPROVED", ct);
        var parameterSnapshot = WhatsAppDirectTemplatePolicy.DeserializeParameterSnapshot(
            delivery.TemplateParametersJson);
        if (parameterSnapshot?.Fingerprint is not null &&
            (template is null || !string.Equals(
                parameterSnapshot.Fingerprint,
                template.Fingerprint,
                StringComparison.Ordinal)))
            return new(false, "The approved template changed after this message was queued.",
                phoneNumber, null, 409, "WHATSAPP_TEMPLATE_DRIFT");
        var validatedTemplate = WhatsAppDirectTemplatePolicy.Validate(
            template,
            parameterSnapshot?.Parameters);
        if (validatedTemplate is null)
            return new(false, "Template parameters do not match the approved template.", phoneNumber, null, 422,
                "WHATSAPP_TEMPLATE_PARAMETERS_INVALID");
        var request = new WhatsAppCloudService.TemplateMessageRequest(
            phoneNumber, template!.Name, template.Language, validatedTemplate.ProviderComponents);
        return await context.Cloud.SendTemplateAsync(request, ct);
    }

    private static async Task<WhatsAppCloudService.SendTestMessageResult> SendMediaAsync(
        DispatchContext context,
        LiveSupportMessage supportMessage,
        string phoneNumber,
        CancellationToken ct)
    {
        var attachment = await context.Db.LiveSupportAttachments.AsNoTracking()
            .SingleAsync(item => item.Id == supportMessage.AttachmentId, ct);
        await using var source = await context.AttachmentStorage.OpenReadAsync(attachment.StoragePath, ct);
        WhatsAppOutboundMedia normalized;
        try
        {
            normalized = await context.MediaNormalizer.NormalizeAsync(
                new WhatsAppOutboundMediaSource(
                    supportMessage.Type,
                    attachment.OriginalFileName,
                    attachment.ContentType,
                    attachment.SizeBytes,
                    source),
                ct);
        }
        catch (WhatsAppMediaNormalizationException exception)
        {
            return new(false, exception.Message, phoneNumber, null, exception.StatusCode,
                exception.ErrorCode, exception.IsRetryable);
        }

        var caption = supportMessage.Type == LiveSupportMessageType.Image
            ? supportMessage.Content
            : null;
        var request = new WhatsAppCloudService.MediaMessageRequest(
            phoneNumber, normalized.MediaType, normalized.FileName, normalized.ContentType,
            normalized.Content, caption);
        return await context.Cloud.SendMediaAsync(request, ct);
    }

    private async Task RecoverUnexpectedFailureAsync(Guid messageId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = ResolveContext(scope);
        var delivery = await context.Db.LiveSupportWhatsAppMessages
            .SingleOrDefaultAsync(message => message.Id == messageId && message.Status == "Sending", ct);
        if (delivery is null) return;

        CompleteFailure(delivery, "WHATSAPP_DISPATCH_FAILED", retryable: true);
        await AppendDeliveryEventAsync(context, delivery, ct);
        await context.Db.SaveChangesAsync(ct);
    }

    private async Task FinalizeUncertainDeliverySafelyAsync(Guid messageId, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = ResolveContext(scope);
            await FinalizeUncertainDeliveryAsync(context, messageId, DateTime.UtcNow, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "WhatsApp uncertain delivery {DeliveryId} could not be finalized.", messageId);
        }
    }

    private static async Task<bool> FinalizeStaleSendingAsync(
        DispatchContext context,
        Guid messageId,
        DateTime staleClaim,
        DateTime now,
        CancellationToken ct)
    {
        return await FinalizeUncertainDeliveryAsync(
            context,
            messageId,
            now,
            message => message.ClaimedAt < staleClaim,
            ct);
    }

    private static Task<bool> FinalizeUncertainDeliveryAsync(
        DispatchContext context,
        Guid messageId,
        DateTime now,
        CancellationToken ct) =>
        FinalizeUncertainDeliveryAsync(context, messageId, now, _ => true, ct);

    private static async Task<bool> FinalizeUncertainDeliveryAsync(
        DispatchContext context,
        Guid messageId,
        DateTime now,
        System.Linq.Expressions.Expression<Func<LiveSupportWhatsAppMessage, bool>> extraCondition,
        CancellationToken ct)
    {
        var updated = await context.Db.LiveSupportWhatsAppMessages
            .Where(message => message.Id == messageId && message.Status == "Sending")
            .Where(extraCondition)
            .ExecuteUpdateAsync(update => update
                .SetProperty(message => message.Status, "Failed")
                .SetProperty(message => message.FailureCode, "WHATSAPP_DELIVERY_UNCERTAIN")
                .SetProperty(message => message.ClaimedAt, (DateTime?)null)
                .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                .SetProperty(message => message.UpdatedAt, now)
                .SetProperty(message => message.Version, message => message.Version + 1), ct);
        if (updated == 0) return false;
        var delivery = await context.Db.LiveSupportWhatsAppMessages.SingleAsync(message => message.Id == messageId, ct);
        await AppendDeliveryEventAsync(context, delivery, ct);
        await context.Db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task FinalizeExhaustedAsync(
        DispatchContext context,
        Guid messageId,
        DateTime now,
        CancellationToken ct)
    {
        var delivery = await context.Db.LiveSupportWhatsAppMessages
            .SingleOrDefaultAsync(message => message.Id == messageId, ct);
        if (delivery is null || delivery.AttemptCount < MaxAttempts || delivery.Direction != "Outbound") return;
        var eligible = delivery.Status == "Pending" &&
                       (!delivery.NextAttemptAt.HasValue || delivery.NextAttemptAt <= now);
        if (!eligible) return;

        CompleteFailure(delivery, "WHATSAPP_MAX_ATTEMPTS_EXCEEDED", retryable: false);
        await AppendDeliveryEventAsync(context, delivery, ct);
        await context.Db.SaveChangesAsync(ct);
    }

    private static Task AppendDeliveryEventAsync(
        DispatchContext context,
        LiveSupportWhatsAppMessage delivery,
        CancellationToken ct) =>
        context.EventWriter.AppendAsync(new LiveSupportEventWriteRequest(
            delivery.ConversationId,
            LiveSupportEventType.WhatsAppDeliveryStatusChanged,
            RelatedEntityId: delivery.LiveSupportMessageId,
            SafeMetadataJson: JsonSerializer.Serialize(new
            {
                messageId = delivery.LiveSupportMessageId,
                status = delivery.Status,
                deliveredAt = delivery.DeliveredAt,
                readAt = delivery.ReadAt,
                failureCode = delivery.FailureCode
            })), ct);

    private static Task<bool> DeliveryAdvancedAfterProviderSaveAsync(
        DispatchContext context,
        LiveSupportWhatsAppMessage delivery,
        CancellationToken ct) =>
        context.Db.LiveSupportWhatsAppMessages.AsNoTracking().AnyAsync(item =>
            item.Id == delivery.Id && item.Version != delivery.Version, ct);

    private static void CompleteAttempt(
        LiveSupportWhatsAppMessage delivery,
        WhatsAppCloudService.SendTestMessageResult response)
    {
        if (response.Success && !string.IsNullOrWhiteSpace(response.MetaMessageId))
        {
            delivery.ClaimedAt = null;
            delivery.NextAttemptAt = null;
            delivery.UpdatedAt = DateTime.UtcNow;
            delivery.Version++;
            delivery.MetaMessageId = response.MetaMessageId;
            delivery.Status = "Sent";
            delivery.FailureCode = null;
            // ProviderTimestamp is receipt authority, not the local HTTP acceptance time.
            // Leaving it null allows a subsequent Meta sent/failed event to order correctly.
        }
        else
        {
            var code = response.Success
                ? "WHATSAPP_CLOUD_INVALID_RESPONSE"
                : response.ErrorCode ?? "WHATSAPP_CLOUD_REQUEST_FAILED";
            CompleteFailure(delivery, code, response.IsRetryable);
        }
    }

    private static void CompleteFailure(
        LiveSupportWhatsAppMessage delivery,
        string failureCode,
        bool retryable)
    {
        var now = DateTime.UtcNow;
        delivery.ClaimedAt = null;
        delivery.UpdatedAt = now;
        delivery.FailureCode = SafeFailureCode(failureCode);
        delivery.Version++;
        if (retryable && delivery.AttemptCount < MaxAttempts)
        {
            delivery.Status = "Pending";
            delivery.NextAttemptAt = now.AddSeconds(Math.Pow(2, delivery.AttemptCount));
        }
        else
        {
            delivery.Status = "Failed";
            delivery.NextAttemptAt = null;
        }
    }

    private static string SafeFailureCode(string failureCode)
    {
        var safe = new string(failureCode
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            .Take(120)
            .ToArray());
        return safe.Length == 0 ? "WHATSAPP_DISPATCH_FAILED" : safe;
    }

}
