using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using Npgsql;

namespace NaderGorge.Infrastructure.Services;

public sealed class FacebookMessengerLiveSupportService(
    AppDbContext db,
    ILiveSupportService support,
    ILiveSupportHumanConversationFactory humanConversations,
    ILiveSupportAttachmentStorage attachmentStorage,
    IFacebookMessengerRuntimeConfigurationReader configurationReader,
    FacebookMessengerWebhookParser webhookParser,
    FacebookMessengerGraphClient graphClient)
{
    private const string InboxPending = "Pending";
    private const string InboxProcessing = "Processing";
    private const string InboxCompleted = "Completed";
    private static readonly TimeSpan StandardReplyWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan HumanAgentReplyWindow = TimeSpan.FromDays(7);

    private DbSet<LiveSupportMessengerBinding> Bindings => db.Set<LiveSupportMessengerBinding>();
    private DbSet<LiveSupportMessengerMessage> MessengerMessages => db.Set<LiveSupportMessengerMessage>();
    private DbSet<LiveSupportMessengerWebhookInbox> WebhookInbox => db.Set<LiveSupportMessengerWebhookInbox>();

    public async Task<int> EnqueueWebhookAsync(JsonElement webhook, CancellationToken ct)
    {
        var configuration = await configurationReader.GetAsync(ct);
        if (!configuration.IsEnabled) return 0;
        var parsedEvents = webhookParser.Parse(
            webhook,
            configuration.Pages.Keys.ToHashSet(StringComparer.Ordinal));
        var inserted = 0;
        foreach (var parsedEvent in parsedEvents)
            if (await EnqueueEventAsync(parsedEvent, ct)) inserted++;
        return inserted;
    }

    private async Task<bool> EnqueueEventAsync(
        FacebookMessengerWebhookEvent parsedEvent,
        CancellationToken ct)
    {
        var existing = await FindInboxAsync(parsedEvent, ct);
        if (existing is not null)
        {
            EnsureSameWebhook(existing, parsedEvent);
            return false;
        }

        var inbox = ToInbox(parsedEvent);
        WebhookInbox.Add(inbox);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.Entry(inbox).State = EntityState.Detached;
            existing = await FindInboxAsync(parsedEvent, ct);
            if (existing is null) throw;
            EnsureSameWebhook(existing, parsedEvent);
            return false;
        }
    }

    public async Task ProcessInboxEventAsync(Guid inboxId, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var inbox = await WebhookInbox.SingleAsync(candidate => candidate.Id == inboxId, ct);
        if (inbox.Status == InboxCompleted) return;
        if (inbox.Status != InboxProcessing)
            throw new FacebookMessengerWebhookException("MESSENGER_INBOX_NOT_CLAIMED", true);
        var configuration = await configurationReader.GetAsync(ct);
        configuration.RequirePage(inbox.PageId);
        using var document = ParseInboxPayload(inbox.PayloadJson);
        await DispatchInboxEventAsync(inbox, document.RootElement, ct);
        inbox.Status = InboxCompleted;
        inbox.ProcessedAt = DateTime.UtcNow;
        inbox.ClaimedAt = null;
        inbox.FailureCode = null;
        inbox.UpdatedAt = inbox.ProcessedAt;
        inbox.Version++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task DispatchInboxEventAsync(
        LiveSupportMessengerWebhookInbox inbox,
        JsonElement messagingEvent,
        CancellationToken ct)
    {
        switch (inbox.EventKind)
        {
            case "message":
                await IngestMessageAsync(inbox.PageId, messagingEvent, ct);
                break;
            case "message_echo":
                await ApplyEchoAsync(inbox.PageId, messagingEvent, ct);
                break;
            case "postback":
                await IngestPostbackAsync(inbox, messagingEvent, ct);
                break;
            case "delivery":
                await ApplyDeliveryAsync(inbox.PageId, messagingEvent, ct);
                break;
            case "read":
                await ApplyReadAsync(inbox.PageId, messagingEvent, ct);
                break;
        }
    }

    private async Task IngestMessageAsync(
        string pageId,
        JsonElement messagingEvent,
        CancellationToken ct)
    {
        var message = RequiredObject(messagingEvent, "message");
        var providerMessageId = CanonicalProviderMessageId(RequiredText(message, "mid"));
        var senderPsid = RequiredNestedText(messagingEvent, "sender", "id");
        await AcquireIdentityLockAsync(pageId, senderPsid, ct);
        if (await MessengerMessages.AnyAsync(candidate =>
                candidate.PageId == pageId &&
                candidate.ProviderMessageId == providerMessageId, ct)) return;
        var providerTimestamp = InboundTimestamp(messagingEvent);
        var displayName = await DisplayNameAsync(pageId, senderPsid, ct);
        var content = await MessageContentAsync(pageId, message, ct);
        await PersistInboundAsync(new MessengerInboundRequest(
            pageId,
            senderPsid,
            displayName,
            providerMessageId,
            providerTimestamp,
            content.Content,
            content.Type,
            content.AttachmentId,
            content.ProviderType), ct);
    }

    private async Task IngestPostbackAsync(
        LiveSupportMessengerWebhookInbox inbox,
        JsonElement messagingEvent,
        CancellationToken ct)
    {
        var postback = RequiredObject(messagingEvent, "postback");
        var senderPsid = RequiredNestedText(messagingEvent, "sender", "id");
        await AcquireIdentityLockAsync(inbox.PageId, senderPsid, ct);
        var providerMessageId = CanonicalProviderMessageId(
            FacebookMessengerWebhookParser.Text(postback, "mid") ?? inbox.DeduplicationKey);
        if (await MessengerMessages.AnyAsync(candidate =>
                candidate.PageId == inbox.PageId &&
                candidate.ProviderMessageId == providerMessageId, ct)) return;
        var content = FacebookMessengerWebhookParser.Text(postback, "title") ??
            RequiredText(postback, "payload");
        var displayName = await DisplayNameAsync(inbox.PageId, senderPsid, ct);
        await PersistInboundAsync(new MessengerInboundRequest(
            inbox.PageId,
            senderPsid,
            displayName,
            providerMessageId,
            InboundTimestamp(messagingEvent),
            CanonicalContent(content, "تفاعل Messenger"),
            LiveSupportMessageType.Text,
            null,
            "postback"), ct);
    }

    private async Task PersistInboundAsync(
        MessengerInboundRequest request,
        CancellationToken ct)
    {
        var conversationContext = await ConversationContextAsync(request, ct);
        if (conversationContext.Conversation.AllowsAI)
            throw new FacebookMessengerWebhookException("MESSENGER_HUMAN_ONLY_VIOLATION", false);
        var participant = new LiveSupportParticipantIdentity(
            LiveSupportParticipantType.Guest,
            null,
            conversationContext.Binding.GuestSessionId);
        var send = await support.IngestExternalMessageAsync(new LiveSupportExternalMessage(
            participant,
            conversationContext.Conversation.Id,
            CanonicalClientMessageId(request.ProviderMessageId),
            request.Content,
            request.Type,
            request.AttachmentId), ct);
        if (!await MessengerMessages.AnyAsync(candidate =>
                candidate.PageId == request.PageId &&
                candidate.ProviderMessageId == request.ProviderMessageId, ct))
            MessengerMessages.Add(InboundDelivery(request, send.Message.Id, conversationContext.Conversation.Id));
        await db.SaveChangesAsync(ct);
    }

    private async Task<MessengerConversationContext> ConversationContextAsync(
        MessengerInboundRequest request,
        CancellationToken ct)
    {
        var configuration = await configurationReader.GetAsync(ct);
        var page = configuration.RequirePage(request.PageId);
        var openBinding = await OpenBindingAsync(request.PageId, request.SenderPsid, ct);
        if (openBinding is not null)
        {
            AdvanceBinding(openBinding, request, page);
            var conversation = await db.LiveSupportConversations
                .SingleAsync(candidate => candidate.Id == openBinding.ConversationId, ct);
            if (conversation.AllowsAI)
                throw new FacebookMessengerWebhookException("MESSENGER_HUMAN_ONLY_VIOLATION", false);
            return new MessengerConversationContext(conversation, openBinding);
        }
        var recovered = await RecoverOpenConversationContextAsync(request, page, ct);
        if (recovered is not null) return recovered;
        return await CreateConversationContextAsync(request, page, ct);
    }

    private async Task<MessengerConversationContext?> RecoverOpenConversationContextAsync(
        MessengerInboundRequest request,
        FacebookMessengerPageConfiguration page,
        CancellationToken ct)
    {
        var closedBindingContext = await (
            from candidateBinding in Bindings
            join conversation in db.LiveSupportConversations
                on candidateBinding.ConversationId equals conversation.Id
            where candidateBinding.PageId == request.PageId &&
                candidateBinding.SenderPsid == request.SenderPsid &&
                !candidateBinding.IsOpen &&
                !conversation.AllowsAI &&
                conversation.Status != LiveSupportConversationStatus.Closed &&
                conversation.Status != LiveSupportConversationStatus.Abandoned
            orderby conversation.CreatedAt descending
            select new { Binding = candidateBinding, Conversation = conversation })
            .FirstOrDefaultAsync(ct);
        if (closedBindingContext is not null)
        {
            closedBindingContext.Binding.IsOpen = true;
            AdvanceBinding(closedBindingContext.Binding, request, page);
            return new MessengerConversationContext(
                closedBindingContext.Conversation,
                closedBindingContext.Binding);
        }

        var identityHash = ExternalIdentityHash(request.PageId, request.SenderPsid);
        var orphanedContext = await (
            from guest in db.LiveSupportGuestSessions
            join conversation in db.LiveSupportConversations
                on guest.Id equals conversation.GuestSessionId
            where guest.CreatedIpHash == identityHash &&
                !conversation.AllowsAI &&
                conversation.Status != LiveSupportConversationStatus.Closed &&
                conversation.Status != LiveSupportConversationStatus.Abandoned &&
                !Bindings.Any(binding => binding.ConversationId == conversation.Id)
            orderby conversation.CreatedAt descending
            select new { Guest = guest, Conversation = conversation })
            .FirstOrDefaultAsync(ct);
        if (orphanedContext is null) return null;

        orphanedContext.Guest.DisplayName = request.DisplayName;
        orphanedContext.Guest.LastSeenAt = DateTime.UtcNow;
        var binding = NewBinding(
            request,
            orphanedContext.Guest.Id,
            orphanedContext.Conversation.Id,
            page);
        Bindings.Add(binding);
        return new MessengerConversationContext(orphanedContext.Conversation, binding);
    }

    private Task<LiveSupportMessengerBinding?> OpenBindingAsync(
        string pageId,
        string senderPsid,
        CancellationToken ct) =>
        (from binding in Bindings
         join conversation in db.LiveSupportConversations
             on binding.ConversationId equals conversation.Id
         where binding.PageId == pageId &&
             binding.SenderPsid == senderPsid &&
             binding.IsOpen &&
             conversation.Status != LiveSupportConversationStatus.Closed &&
             conversation.Status != LiveSupportConversationStatus.Abandoned
         orderby binding.LastInboundAt descending
         select binding).FirstOrDefaultAsync(ct);

    private async Task<MessengerConversationContext> CreateConversationContextAsync(
        MessengerInboundRequest request,
        FacebookMessengerPageConfiguration page,
        CancellationToken ct)
    {
        var previousBinding = await Bindings
            .Where(binding => binding.PageId == request.PageId && binding.SenderPsid == request.SenderPsid)
            .OrderByDescending(binding => binding.LastInboundAt)
            .FirstOrDefaultAsync(ct);
        var guest = previousBinding is null
            ? await CreateGuestAsync(request, ct)
            : await ExistingGuestAsync(previousBinding.GuestSessionId, request.DisplayName, ct);
        var previousConversationId = previousBinding?.ConversationId;
        if (previousBinding is not null) previousBinding.IsOpen = false;
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guest.Id);
        var created = await humanConversations.CreateHumanOnlyAsync(
            participant,
            null,
            previousConversationId,
            ct);
        var conversation = await db.LiveSupportConversations.SingleAsync(candidate => candidate.Id == created.Id, ct);
        if (conversation.AllowsAI)
            throw new FacebookMessengerWebhookException("MESSENGER_HUMAN_ONLY_VIOLATION", false);
        var binding = NewBinding(request, guest.Id, conversation.Id, page);
        Bindings.Add(binding);
        await db.SaveChangesAsync(ct);
        return new MessengerConversationContext(conversation, binding);
    }

    private async Task<LiveSupportGuestSession> CreateGuestAsync(
        MessengerInboundRequest request,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var guest = new LiveSupportGuestSession
        {
            DisplayName = request.DisplayName,
            PhoneNumber = null,
            SecurityStampHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            CreatedIpHash = ExternalIdentityHash(request.PageId, request.SenderPsid),
            ExpiresAt = now.AddYears(10),
            LastSeenAt = now,
            UserAgentSummary = "Facebook Messenger Platform"
        };
        db.LiveSupportGuestSessions.Add(guest);
        await db.SaveChangesAsync(ct);
        return guest;
    }

    private async Task<LiveSupportGuestSession> ExistingGuestAsync(
        Guid guestSessionId,
        string displayName,
        CancellationToken ct)
    {
        var guest = await db.LiveSupportGuestSessions.SingleAsync(candidate => candidate.Id == guestSessionId, ct);
        guest.DisplayName = displayName;
        guest.LastSeenAt = DateTime.UtcNow;
        return guest;
    }

    private async Task<string> DisplayNameAsync(
        string pageId,
        string senderPsid,
        CancellationToken ct)
    {
        var fallback = await Bindings.AsNoTracking()
            .Where(binding => binding.PageId == pageId && binding.SenderPsid == senderPsid)
            .OrderByDescending(binding => binding.LastInboundAt)
            .Select(binding => binding.DisplayName)
            .FirstOrDefaultAsync(ct) ?? "مستخدم Messenger";
        var displayName = await graphClient.ProfileDisplayNameAsync(pageId, senderPsid, fallback, ct);
        displayName = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();
        return TruncateWithoutSplittingSurrogate(displayName, 120);
    }

    private async Task<MessengerInboundContent> MessageContentAsync(
        string pageId,
        JsonElement message,
        CancellationToken ct)
    {
        if (FacebookMessengerWebhookParser.Text(message, "text") is { } text)
            return new MessengerInboundContent(
                CanonicalContent(text, "رسالة Messenger"),
                LiveSupportMessageType.Text,
                null,
                "text");
        var attachment = FacebookMessengerWebhookParser.Array(message, "attachments").FirstOrDefault();
        if (attachment.ValueKind != JsonValueKind.Object)
            return UnsupportedContent("unknown");
        return await AttachmentContentAsync(pageId, attachment, ct);
    }

    private async Task<MessengerInboundContent> AttachmentContentAsync(
        string pageId,
        JsonElement attachment,
        CancellationToken ct)
    {
        var providerType = FacebookMessengerWebhookParser.Text(attachment, "type") ?? "unknown";
        if (providerType is not ("image" or "audio" or "file")) return UnsupportedContent(providerType);
        if (!attachment.TryGetProperty("payload", out var payload) ||
            FacebookMessengerWebhookParser.Text(payload, "url") is not { } mediaUrl)
            return UnsupportedContent(providerType);
        FacebookMessengerDownloadedMedia downloaded;
        try
        {
            downloaded = await graphClient.DownloadInboundMediaAsync(pageId, mediaUrl, ct);
        }
        catch (FacebookMessengerProviderException exception) when (!exception.IsRetryable)
        {
            return UnavailableContent(providerType);
        }
        var messageType = SupportedMessageType(providerType, downloaded.ContentType);
        if (!messageType.HasValue) return UnsupportedContent(providerType);
        await using var content = new MemoryStream(downloaded.Content, writable: false);
        LiveSupportStoredAttachment stored;
        try
        {
            stored = await attachmentStorage.SaveAsync(
                content,
                downloaded.FileName,
                downloaded.ContentType,
                downloaded.Content.LongLength,
                ct);
        }
        catch (InvalidUploadContentException)
        {
            return UnavailableContent(providerType);
        }
        var entity = new LiveSupportAttachment
        {
            StoragePath = stored.StoragePath,
            OriginalFileName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            Sha256 = stored.Sha256,
            UploadedByIdentity = "messenger"
        };
        db.LiveSupportAttachments.Add(entity);
        await db.SaveChangesAsync(ct);
        return new MessengerInboundContent(stored.OriginalFileName, messageType.Value, entity.Id, providerType);
    }

    private async Task ApplyEchoAsync(
        string pageId,
        JsonElement messagingEvent,
        CancellationToken ct)
    {
        var message = RequiredObject(messagingEvent, "message");
        var providerMessageId = CanonicalProviderMessageId(RequiredText(message, "mid"));
        var delivery = await MessengerMessages.SingleOrDefaultAsync(candidate =>
            candidate.PageId == pageId &&
            candidate.ProviderMessageId == providerMessageId &&
            candidate.Direction == "Outbound", ct);
        if (delivery is null)
        {
            var senderPsid = RequiredNestedText(messagingEvent, "recipient", "id");
            await RequireNoSendingDeliveryAsync(pageId, senderPsid, ct);
            return;
        }
        var observedAt = ProviderTimestamp(messagingEvent);
        delivery.Status = SuccessRank(delivery.Status) >= SuccessRank("Sent") ? delivery.Status : "Sent";
        delivery.ProviderTimestamp = Later(delivery.ProviderTimestamp, observedAt);
        delivery.FailureCode = null;
        delivery.UpdatedAt = DateTime.UtcNow;
        delivery.Version++;
        await db.SaveChangesAsync(ct);
    }

    private Task ApplyDeliveryAsync(
        string pageId,
        JsonElement messagingEvent,
        CancellationToken ct) =>
        ApplyReceiptAsync(pageId, messagingEvent, "delivery", "Delivered", ct);

    private Task ApplyReadAsync(
        string pageId,
        JsonElement messagingEvent,
        CancellationToken ct) =>
        ApplyReceiptAsync(pageId, messagingEvent, "read", "Read", ct);

    private async Task ApplyReceiptAsync(
        string pageId,
        JsonElement messagingEvent,
        string receiptProperty,
        string targetStatus,
        CancellationToken ct)
    {
        var senderPsid = RequiredNestedText(messagingEvent, "sender", "id");
        var receipt = RequiredObject(messagingEvent, receiptProperty);
        var watermark = RequiredNumber(receipt, "watermark");
        var observedAt = UnixMilliseconds(watermark, "MESSENGER_RECEIPT_TIMESTAMP_INVALID");
        var messageIds = FacebookMessengerWebhookParser.Array(receipt, "mids")
            .Where(mid => mid.ValueKind == JsonValueKind.String)
            .Select(mid => CanonicalProviderMessageId(mid.GetString()!))
            .ToArray();
        var deliveries = await MessengerMessages.Where(candidate =>
                candidate.PageId == pageId &&
                candidate.SenderPsid == senderPsid &&
                candidate.Direction == "Outbound" &&
                (messageIds.Length == 0
                    ? candidate.CreatedAt <= observedAt &&
                      (candidate.Status == "Sending" ||
                       candidate.Status == "Sent" ||
                       candidate.Status == "Delivered" ||
                       candidate.Status == "Read" ||
                       candidate.FailureCode == "MESSENGER_DELIVERY_UNCERTAIN")
                    : candidate.ProviderMessageId != null && messageIds.Contains(candidate.ProviderMessageId)))
            .ToListAsync(ct);
        if (deliveries.Count == 0)
            await RequireNoSendingDeliveryAsync(pageId, senderPsid, ct);
        foreach (var delivery in deliveries)
            await AdvanceReceiptAsync(delivery, targetStatus, observedAt, ct);
        if (deliveries.Count > 0) await db.SaveChangesAsync(ct);
    }

    private async Task AdvanceReceiptAsync(
        LiveSupportMessengerMessage delivery,
        string targetStatus,
        DateTime observedAt,
        CancellationToken ct)
    {
        if (SuccessRank(delivery.Status) > SuccessRank(targetStatus)) return;
        delivery.Status = targetStatus;
        delivery.ProviderTimestamp = Later(delivery.ProviderTimestamp, observedAt);
        delivery.DeliveredAt = Earlier(delivery.DeliveredAt, observedAt);
        if (targetStatus == "Read") delivery.ReadAt = Earlier(delivery.ReadAt, observedAt);
        delivery.FailureCode = null;
        delivery.UpdatedAt = DateTime.UtcNow;
        delivery.Version++;
        if (!delivery.LiveSupportMessageId.HasValue) return;
        var canonical = await db.LiveSupportMessages.SingleOrDefaultAsync(
            message => message.Id == delivery.LiveSupportMessageId.Value, ct);
        if (canonical is null) return;
        canonical.DeliveredAt = Earlier(canonical.DeliveredAt, observedAt);
        if (targetStatus == "Read") canonical.ReadAt = Earlier(canonical.ReadAt, observedAt);
    }

    private static LiveSupportMessengerWebhookInbox ToInbox(FacebookMessengerWebhookEvent parsedEvent) =>
        new()
        {
            PageId = parsedEvent.PageId,
            EventKind = parsedEvent.EventKind,
            DeduplicationKey = parsedEvent.DeduplicationKey,
            PayloadHash = parsedEvent.PayloadHash,
            PayloadJson = parsedEvent.PayloadJson,
            Status = InboxPending,
            Version = 1
        };

    private LiveSupportMessengerBinding NewBinding(
        MessengerInboundRequest request,
        Guid guestSessionId,
        Guid conversationId,
        FacebookMessengerPageConfiguration page)
    {
        return new LiveSupportMessengerBinding
        {
            ConversationId = conversationId,
            GuestSessionId = guestSessionId,
            PageId = page.PageId,
            PageName = page.DisplayName,
            SenderPsid = request.SenderPsid,
            DisplayName = request.DisplayName,
            IsOpen = true,
            LastInboundAt = request.ProviderTimestamp,
            ReplyWindowExpiresAt = ReplyWindowExpiresAt(page, request.ProviderTimestamp),
            Version = 1
        };
    }

    private void AdvanceBinding(
        LiveSupportMessengerBinding binding,
        MessengerInboundRequest request,
        FacebookMessengerPageConfiguration page)
    {
        binding.PageName = page.DisplayName;
        binding.DisplayName = request.DisplayName;
        if (request.ProviderTimestamp > binding.LastInboundAt)
        {
            binding.LastInboundAt = request.ProviderTimestamp;
            binding.ReplyWindowExpiresAt = ReplyWindowExpiresAt(page, request.ProviderTimestamp);
        }
        binding.UpdatedAt = DateTime.UtcNow;
        binding.Version++;
    }

    private static DateTime ReplyWindowExpiresAt(
        FacebookMessengerPageConfiguration page,
        DateTime inboundAt) =>
        inboundAt + (page.HumanAgentEnabled ? HumanAgentReplyWindow : StandardReplyWindow);

    private static LiveSupportMessengerMessage InboundDelivery(
        MessengerInboundRequest request,
        Guid canonicalMessageId,
        Guid conversationId) =>
        new()
        {
            ConversationId = conversationId,
            LiveSupportMessageId = canonicalMessageId,
            PageId = request.PageId,
            SenderPsid = request.SenderPsid,
            ProviderMessageId = request.ProviderMessageId,
            Direction = "Inbound",
            MessageType = request.ProviderType,
            Status = "Received",
            ProviderTimestamp = request.ProviderTimestamp,
            Version = 1
        };

    private static MessengerInboundContent UnsupportedContent(string providerType) =>
        new($"رسالة Messenger من النوع: {providerType}", LiveSupportMessageType.Text, null, providerType);

    private static MessengerInboundContent UnavailableContent(string providerType) =>
        new("تعذر استلام مرفق Messenger لأنه غير متاح أو غير مدعوم.", LiveSupportMessageType.Text, null, providerType);

    private static LiveSupportMessageType? SupportedMessageType(string providerType, string contentType) =>
        providerType switch
        {
            "image" when contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) =>
                LiveSupportMessageType.Image,
            "audio" when contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) =>
                LiveSupportMessageType.Audio,
            "file" when string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase) =>
                LiveSupportMessageType.Pdf,
            _ => null
        };

    private static string CanonicalClientMessageId(string providerMessageId) =>
        providerMessageId.Length <= 97
            ? $"fb:{providerMessageId}"
            : Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(providerMessageId))).ToLowerInvariant();

    private static string CanonicalContent(string content, string fallback)
    {
        var normalized = content.Trim();
        if (normalized.Length == 0) return fallback;
        return TruncateWithoutSplittingSurrogate(normalized, 4000);
    }

    private static string TruncateWithoutSplittingSurrogate(string text, int maximumLength)
    {
        if (text.Length <= maximumLength) return text;
        var safeLength = maximumLength;
        if (char.IsHighSurrogate(text[safeLength - 1]) && char.IsLowSurrogate(text[safeLength]))
            safeLength--;
        return text[..safeLength];
    }

    private static string CanonicalProviderMessageId(string providerMessageId) =>
        providerMessageId.Length <= 256
            ? providerMessageId
            : $"sha256:{Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(providerMessageId))).ToLowerInvariant()}";

    private static string ExternalIdentityHash(string pageId, string senderPsid) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"messenger:{pageId}:{senderPsid}"))).ToLowerInvariant();

    private static JsonDocument ParseInboxPayload(string payloadJson)
    {
        try
        {
            return JsonDocument.Parse(payloadJson, new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (JsonException)
        {
            throw new FacebookMessengerWebhookException("MESSENGER_INBOX_PAYLOAD_INVALID", false);
        }
    }

    private static JsonElement RequiredObject(JsonElement element, string property) =>
        element.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? nested
            : throw new FacebookMessengerWebhookException("MESSENGER_WEBHOOK_SHAPE_INVALID", false);

    private static string RequiredText(JsonElement element, string property) =>
        FacebookMessengerWebhookParser.Text(element, property) is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new FacebookMessengerWebhookException("MESSENGER_WEBHOOK_SHAPE_INVALID", false);

    private static string RequiredNestedText(
        JsonElement element,
        string container,
        string property) =>
        FacebookMessengerWebhookParser.NestedText(element, container, property) is { } text &&
        !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new FacebookMessengerWebhookException("MESSENGER_WEBHOOK_SHAPE_INVALID", false);

    private static long RequiredNumber(JsonElement element, string property) =>
        FacebookMessengerWebhookParser.Number(element, property) ??
        throw new FacebookMessengerWebhookException("MESSENGER_WEBHOOK_SHAPE_INVALID", false);

    private static DateTime ProviderTimestamp(JsonElement messagingEvent)
    {
        var milliseconds = RequiredNumber(messagingEvent, "timestamp");
        return UnixMilliseconds(milliseconds, "MESSENGER_WEBHOOK_TIMESTAMP_INVALID");
    }

    private static DateTime InboundTimestamp(JsonElement messagingEvent)
    {
        var providerTimestamp = ProviderTimestamp(messagingEvent);
        var receivedAt = DateTime.UtcNow;
        return providerTimestamp <= receivedAt.AddMinutes(5) ? providerTimestamp : receivedAt;
    }

    private static DateTime UnixMilliseconds(long milliseconds, string failureCode)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new FacebookMessengerWebhookException(failureCode, false);
        }
    }

    private static DateTime Later(DateTime? current, DateTime candidate) =>
        !current.HasValue || candidate > current.Value ? candidate : current.Value;

    private static DateTime Earlier(DateTime? current, DateTime candidate) =>
        !current.HasValue || candidate < current.Value ? candidate : current.Value;

    private async Task AcquireIdentityLockAsync(
        string pageId,
        string senderPsid,
        CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{pageId}\n{senderPsid}"));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(digest);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            ct);
    }

    private Task<LiveSupportMessengerWebhookInbox?> FindInboxAsync(
        FacebookMessengerWebhookEvent parsedEvent,
        CancellationToken ct) =>
        WebhookInbox.AsNoTracking().SingleOrDefaultAsync(inbox =>
            inbox.PageId == parsedEvent.PageId &&
            inbox.DeduplicationKey == parsedEvent.DeduplicationKey,
            ct);

    private async Task RequireNoSendingDeliveryAsync(
        string pageId,
        string senderPsid,
        CancellationToken ct)
    {
        if (await MessengerMessages.AnyAsync(candidate =>
                candidate.PageId == pageId &&
                candidate.SenderPsid == senderPsid &&
                candidate.Direction == "Outbound" &&
                candidate.Status == "Sending", ct))
            throw new FacebookMessengerWebhookException("MESSENGER_RECEIPT_TARGET_PENDING", true);
    }

    private static void EnsureSameWebhook(
        LiveSupportMessengerWebhookInbox existing,
        FacebookMessengerWebhookEvent parsedEvent)
    {
        if (!string.Equals(existing.PayloadHash, parsedEvent.PayloadHash, StringComparison.Ordinal))
            throw new FacebookMessengerWebhookException(
                "MESSENGER_WEBHOOK_IDEMPOTENCY_CONFLICT",
                false);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static int SuccessRank(string? status) => status switch
    {
        "Sent" => 1,
        "Delivered" => 2,
        "Read" => 3,
        _ => 0
    };

    private sealed record MessengerInboundRequest(
        string PageId,
        string SenderPsid,
        string DisplayName,
        string ProviderMessageId,
        DateTime ProviderTimestamp,
        string Content,
        LiveSupportMessageType Type,
        Guid? AttachmentId,
        string ProviderType);

    private sealed record MessengerInboundContent(
        string Content,
        LiveSupportMessageType Type,
        Guid? AttachmentId,
        string ProviderType);

    private sealed record MessengerConversationContext(
        LiveSupportConversation Conversation,
        LiveSupportMessengerBinding Binding);
}

public sealed class FacebookMessengerWebhookException(
    string errorCode,
    bool isRetryable)
    : Exception("Facebook Messenger webhook processing failed.")
{
    public string ErrorCode { get; } = errorCode;
    public bool IsRetryable { get; } = isRetryable;
}
