using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.BackgroundServices;

public sealed class FacebookMessengerOutboundBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<FacebookMessengerOutboundBackgroundService> logger) : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StandardReplyWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan HumanAgentReplyWindow = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunBatchSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunBatchSafelyAsync(stoppingToken);
    }

    internal async Task DispatchBatchAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now - StaleClaimAge;
        List<Guid> candidateIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            candidateIds = await db.LiveSupportMessengerMessages.AsNoTracking()
                .Where(message => message.Direction == "Outbound" &&
                    ((message.Status == "Pending" &&
                      (message.NextAttemptAt == null || message.NextAttemptAt <= now)) ||
                     (message.Status == "Sending" && message.ClaimedAt < staleBefore)) &&
                    !db.LiveSupportMessengerMessages.Any(previous =>
                        previous.ConversationId == message.ConversationId &&
                        previous.Direction == "Outbound" &&
                        (previous.Status == "Pending" || previous.Status == "Sending") &&
                        (previous.CreatedAt < message.CreatedAt ||
                         previous.CreatedAt == message.CreatedAt &&
                         previous.Id.CompareTo(message.Id) < 0)))
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Select(message => message.Id)
                .Take(20)
                .ToListAsync(ct);
        }

        foreach (var candidateId in candidateIds)
            await DispatchSafelyAsync(candidateId, staleBefore, ct);
    }

    private async Task RunBatchSafelyAsync(CancellationToken ct)
    {
        try
        {
            await DispatchBatchAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Facebook Messenger outbound batch failed.");
        }
    }

    private async Task DispatchSafelyAsync(
        Guid deliveryId,
        DateTime staleBefore,
        CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = ResolveContext(scope);
            var now = DateTime.UtcNow;
            if (await FinalizeStaleClaimAsync(context, deliveryId, staleBefore, now, ct)) return;
            if (await TryClaimAsync(context.Db, deliveryId, now, ct) == 0)
            {
                await FinalizeExhaustedAsync(context, deliveryId, now, ct);
                return;
            }
            await DispatchClaimedAsync(context, deliveryId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Facebook Messenger outbound delivery {DeliveryId} failed unexpectedly.",
                deliveryId);
            await FinalizeUnexpectedFailureSafelyAsync(deliveryId, ct);
        }
    }

    private static MessengerDispatchContext ResolveContext(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
        scope.ServiceProvider.GetRequiredService<FacebookMessengerGraphClient>(),
        scope.ServiceProvider.GetRequiredService<IFacebookMessengerRuntimeConfigurationReader>(),
        scope.ServiceProvider.GetRequiredService<ILiveSupportEventWriter>());

    private static Task<int> TryClaimAsync(
        IAppDbContext db,
        Guid deliveryId,
        DateTime claimedAt,
        CancellationToken ct) =>
        db.LiveSupportMessengerMessages
            .Where(message => message.Id == deliveryId &&
                message.Direction == "Outbound" &&
                message.Status == "Pending" &&
                message.AttemptCount < MaxAttempts &&
                (message.NextAttemptAt == null || message.NextAttemptAt <= claimedAt) &&
                !db.LiveSupportMessengerMessages.Any(previous =>
                    previous.ConversationId == message.ConversationId &&
                    previous.Direction == "Outbound" &&
                    (previous.Status == "Pending" || previous.Status == "Sending") &&
                    (previous.CreatedAt < message.CreatedAt ||
                     previous.CreatedAt == message.CreatedAt &&
                     previous.Id.CompareTo(message.Id) < 0)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(message => message.Status, "Sending")
                .SetProperty(message => message.ClaimedAt, claimedAt)
                .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1)
                .SetProperty(message => message.UpdatedAt, claimedAt)
                .SetProperty(message => message.Version, message => message.Version + 1), ct);

    private async Task DispatchClaimedAsync(
        MessengerDispatchContext context,
        Guid deliveryId,
        CancellationToken ct)
    {
        var delivery = await context.Db.LiveSupportMessengerMessages
            .SingleAsync(message => message.Id == deliveryId, ct);
        var validation = await ValidateDispatchAsync(context, delivery, ct);
        if (validation.FailureCode is not null)
        {
            CompleteFailure(delivery, validation.FailureCode, retryable: false);
            await AppendDeliveryEventAsync(context, delivery, ct);
            await context.Db.SaveChangesAsync(ct);
            return;
        }

        FacebookMessengerSendReceipt receipt;
        try
        {
            receipt = validation.UseHumanAgent
                ? await context.Graph.SendHumanAgentTextAsync(
                    delivery.PageId,
                    delivery.SenderPsid,
                    validation.Message!.Content,
                    ct)
                : await context.Graph.SendTextAsync(
                    delivery.PageId,
                    delivery.SenderPsid,
                    validation.Message!.Content,
                    ct);
        }
        catch (FacebookMessengerDeliveryUncertainException)
        {
            await PersistUncertainAsync(context, delivery, ct);
            return;
        }
        catch (FacebookMessengerProviderException exception)
        {
            CompleteFailure(delivery, exception.ErrorCode, exception.IsRetryable);
            await AppendDeliveryEventAsync(context, delivery, ct);
            await context.Db.SaveChangesAsync(ct);
            return;
        }
        catch (HttpRequestException)
        {
            await PersistUncertainAsync(context, delivery, ct);
            return;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await PersistUncertainAsync(context, delivery, ct);
            return;
        }

        delivery.ProviderMessageId = receipt.ProviderMessageId;
        delivery.Status = "Sent";
        delivery.FailureCode = null;
        delivery.ClaimedAt = null;
        delivery.NextAttemptAt = null;
        delivery.UpdatedAt = DateTime.UtcNow;
        delivery.Version++;
        try
        {
            await AppendDeliveryEventAsync(context, delivery, ct);
            await context.Db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Messenger delivery {DeliveryId} was accepted by Meta but could not be persisted.",
                deliveryId);
            await FinalizeUncertainSafelyAsync(deliveryId, ct);
        }
    }

    private static async Task<MessengerDispatchValidation> ValidateDispatchAsync(
        MessengerDispatchContext context,
        LiveSupportMessengerMessage delivery,
        CancellationToken ct)
    {
        if (!delivery.LiveSupportMessageId.HasValue ||
            !string.Equals(delivery.MessageType, "text", StringComparison.Ordinal))
            return MessengerDispatchValidation.Failed("MESSENGER_MESSAGE_TYPE_UNSUPPORTED");

        var message = await context.Db.LiveSupportMessages.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == delivery.LiveSupportMessageId.Value, ct);
        var conversation = await context.Db.LiveSupportConversations.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == delivery.ConversationId, ct);
        var binding = await context.Db.LiveSupportMessengerBindings.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ConversationId == delivery.ConversationId, ct);
        if (message is null || conversation is null || binding is null)
            return MessengerDispatchValidation.Failed("MESSENGER_DISPATCH_CONTEXT_MISSING");
        if (message.ConversationId != conversation.Id ||
            message.SenderType is not (LiveSupportSenderType.Staff or LiveSupportSenderType.Admin) ||
            !message.SenderUserId.HasValue ||
            message.DeletedAt.HasValue ||
            conversation.ParticipantType != LiveSupportParticipantType.Guest ||
            conversation.GuestSessionId != binding.GuestSessionId ||
            conversation.AllowsAI ||
            !string.Equals(binding.PageId, delivery.PageId, StringComparison.Ordinal) ||
            !string.Equals(binding.SenderPsid, delivery.SenderPsid, StringComparison.Ordinal))
            return MessengerDispatchValidation.Failed("MESSENGER_HUMAN_ONLY_DISPATCH_REJECTED");

        FacebookMessengerPageConfiguration page;
        try
        {
            var configuration = await context.ConfigurationReader.GetAsync(ct);
            page = configuration.RequirePage(delivery.PageId);
        }
        catch (FacebookMessengerConfigurationException exception)
        {
            return MessengerDispatchValidation.Failed(exception.ErrorCode);
        }

        var now = DateTime.UtcNow;
        var standardExpiry = binding.LastInboundAt + StandardReplyWindow;
        var humanAgentExpiry = binding.LastInboundAt + HumanAgentReplyWindow;
        if (now <= standardExpiry && now <= binding.ReplyWindowExpiresAt)
            return MessengerDispatchValidation.Allowed(message, useHumanAgent: false);
        if (page.HumanAgentEnabled &&
            now <= humanAgentExpiry &&
            now <= binding.ReplyWindowExpiresAt)
            return MessengerDispatchValidation.Allowed(message, useHumanAgent: true);
        return MessengerDispatchValidation.Failed("MESSENGER_WINDOW_CLOSED");
    }

    private async Task PersistUncertainAsync(
        MessengerDispatchContext context,
        LiveSupportMessengerMessage delivery,
        CancellationToken ct)
    {
        CompleteFailure(delivery, "MESSENGER_DELIVERY_UNCERTAIN", retryable: false);
        try
        {
            await AppendDeliveryEventAsync(context, delivery, ct);
            await context.Db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Messenger uncertain delivery {DeliveryId} could not be persisted in its dispatch scope.",
                delivery.Id);
            await FinalizeUncertainSafelyAsync(delivery.Id, ct);
        }
    }

    private async Task FinalizeUnexpectedFailureSafelyAsync(Guid deliveryId, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = ResolveContext(scope);
            var delivery = await context.Db.LiveSupportMessengerMessages
                .SingleOrDefaultAsync(message => message.Id == deliveryId && message.Status == "Sending", ct);
            if (delivery is null) return;
            CompleteFailure(delivery, "MESSENGER_DISPATCH_FAILED", retryable: true);
            await AppendDeliveryEventAsync(context, delivery, ct);
            await context.Db.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Messenger delivery {DeliveryId} recovery failed.", deliveryId);
        }
    }

    private async Task FinalizeUncertainSafelyAsync(Guid deliveryId, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = ResolveContext(scope);
            await FinalizeUncertainAsync(context, deliveryId, _ => true, DateTime.UtcNow, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Messenger uncertain delivery {DeliveryId} could not be finalized.", deliveryId);
        }
    }

    private static Task<bool> FinalizeStaleClaimAsync(
        MessengerDispatchContext context,
        Guid deliveryId,
        DateTime staleBefore,
        DateTime now,
        CancellationToken ct) =>
        FinalizeUncertainAsync(
            context,
            deliveryId,
            message => message.ClaimedAt < staleBefore,
            now,
            ct);

    private static async Task<bool> FinalizeUncertainAsync(
        MessengerDispatchContext context,
        Guid deliveryId,
        System.Linq.Expressions.Expression<Func<LiveSupportMessengerMessage, bool>> condition,
        DateTime now,
        CancellationToken ct)
    {
        var updated = await context.Db.LiveSupportMessengerMessages
            .Where(message => message.Id == deliveryId && message.Status == "Sending")
            .Where(condition)
            .ExecuteUpdateAsync(update => update
                .SetProperty(message => message.Status, "Failed")
                .SetProperty(message => message.FailureCode, "MESSENGER_DELIVERY_UNCERTAIN")
                .SetProperty(message => message.ClaimedAt, (DateTime?)null)
                .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                .SetProperty(message => message.UpdatedAt, now)
                .SetProperty(message => message.Version, message => message.Version + 1), ct);
        if (updated == 0) return false;
        var delivery = await context.Db.LiveSupportMessengerMessages
            .SingleAsync(message => message.Id == deliveryId, ct);
        await AppendDeliveryEventAsync(context, delivery, ct);
        await context.Db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task FinalizeExhaustedAsync(
        MessengerDispatchContext context,
        Guid deliveryId,
        DateTime now,
        CancellationToken ct)
    {
        var delivery = await context.Db.LiveSupportMessengerMessages
            .SingleOrDefaultAsync(message => message.Id == deliveryId, ct);
        if (delivery is null ||
            delivery.Direction != "Outbound" ||
            delivery.Status != "Pending" ||
            delivery.AttemptCount < MaxAttempts ||
            delivery.NextAttemptAt > now) return;
        CompleteFailure(delivery, "MESSENGER_MAX_ATTEMPTS_EXCEEDED", retryable: false);
        await AppendDeliveryEventAsync(context, delivery, ct);
        await context.Db.SaveChangesAsync(ct);
    }

    private static Task AppendDeliveryEventAsync(
        MessengerDispatchContext context,
        LiveSupportMessengerMessage delivery,
        CancellationToken ct) =>
        context.EventWriter.AppendAsync(new LiveSupportEventWriteRequest(
            delivery.ConversationId,
            LiveSupportEventType.MessengerDeliveryStatusChanged,
            RelatedEntityId: delivery.LiveSupportMessageId,
            SafeMetadataJson: JsonSerializer.Serialize(new
            {
                messageId = delivery.LiveSupportMessageId,
                status = delivery.Status,
                deliveredAt = delivery.DeliveredAt,
                readAt = delivery.ReadAt,
                failureCode = delivery.FailureCode
            })), ct);

    private static void CompleteFailure(
        LiveSupportMessengerMessage delivery,
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
        return safe.Length == 0 ? "MESSENGER_DISPATCH_FAILED" : safe;
    }

    private sealed record MessengerDispatchContext(
        IAppDbContext Db,
        FacebookMessengerGraphClient Graph,
        IFacebookMessengerRuntimeConfigurationReader ConfigurationReader,
        ILiveSupportEventWriter EventWriter);

    private sealed record MessengerDispatchValidation(
        LiveSupportMessage? Message,
        bool UseHumanAgent,
        string? FailureCode)
    {
        public static MessengerDispatchValidation Allowed(
            LiveSupportMessage message,
            bool useHumanAgent) => new(message, useHumanAgent, null);

        public static MessengerDispatchValidation Failed(string failureCode) =>
            new(null, false, failureCode);
    }
}
