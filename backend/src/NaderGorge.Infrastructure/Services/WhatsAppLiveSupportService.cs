using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class WhatsAppLiveSupportService(
    IAppDbContext db,
    ILiveSupportService support,
    ILiveSupportAttachmentStorage attachmentStorage,
    WhatsAppCloudService cloud,
    ILiveSupportEventWriter eventWriter,
    IConfiguration configuration)
{
    private const string UnsupportedDocumentMessage = "تعذر استلام مرفق واتساب. يُسمح بملفات PDF فقط.";
    private const string UnavailableMediaMessage = "تعذر استلام مرفق واتساب لأن الملف غير متاح أو غير مدعوم.";
    private static readonly TimeSpan PendingReceiptRetention = TimeSpan.FromDays(30);

    public async Task ProcessWebhookAsync(JsonElement payload, CancellationToken ct)
    {
        if (!payload.TryGetProperty("object", out var objectType) || objectType.GetString() != "whatsapp_business_account") return;
        if (!payload.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) return;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!MatchesBusinessAccount(entry)) continue;
            foreach (var change in Array(entry, "changes"))
            {
                if (!change.TryGetProperty("value", out var value)) continue;
                if (!MatchesPhoneNumber(value)) continue;
                foreach (var status in Array(value, "statuses")) await ApplyStatusAsync(status, ct);
                foreach (var message in Array(value, "messages")) await IngestAsync(value, message, ct);
            }
        }
    }

    public async Task<IReadOnlyList<LiveSupportWhatsAppTemplateDto>> SyncTemplatesAsync(CancellationToken ct)
    {
        var snapshots = await cloud.GetTemplatesAsync(ct);
        var now = DateTime.UtcNow;
        var existingTemplates = await db.LiveSupportWhatsAppTemplates.ToListAsync(ct);
        var templatesByMetaId = existingTemplates.ToDictionary(template => template.MetaTemplateId);
        var seenMetaIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            if (!seenMetaIds.Add(snapshot.Id)) continue;
            if (!templatesByMetaId.TryGetValue(snapshot.Id, out var template))
            {
                template = new LiveSupportWhatsAppTemplate { MetaTemplateId = snapshot.Id, Version = 1 };
                db.LiveSupportWhatsAppTemplates.Add(template);
            }
            else template.Version++;
            template.Name = snapshot.Name;
            template.Language = snapshot.Language;
            template.Category = snapshot.Category;
            template.Status = snapshot.Status;
            template.ComponentsJson = snapshot.Components.GetRawText();
            template.LastSyncedAt = now;
            template.UpdatedAt = now;
        }
        foreach (var staleTemplate in existingTemplates.Where(template => !seenMetaIds.Contains(template.MetaTemplateId)))
        {
            staleTemplate.Status = "STALE";
            staleTemplate.LastSyncedAt = now;
            staleTemplate.UpdatedAt = now;
            staleTemplate.Version++;
        }
        await db.SaveChangesAsync(ct);
        return await ListTemplatesAsync(ct);
    }

    public async Task<IReadOnlyList<LiveSupportWhatsAppTemplateDto>> ListTemplatesAsync(CancellationToken ct)
    {
        var templates = await db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .OrderBy(item => item.Name).ThenBy(item => item.Language).ToListAsync(ct);
        return templates.Select(ToTemplateDto).ToList();
    }

    public async Task<bool> ReconcilePendingReceiptAsync(string metaMessageId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(metaMessageId)) return false;
        var pending = await db.LiveSupportWhatsAppPendingReceipts
            .SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        if (pending is null) return false;
        var delivery = await db.LiveSupportWhatsAppMessages
            .SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        if (delivery is null) return false;

        var changed = ApplyPendingReceipt(delivery, pending);
        if (changed) await UpdateCanonicalMessageAsync(delivery, ct);
        await AppendDeliveryEventAsync(delivery, ct);
        db.LiveSupportWhatsAppPendingReceipts.Remove(pending);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> CleanupExpiredPendingReceiptsAsync(DateTime utcNow, CancellationToken ct)
    {
        var expiresBefore = utcNow - PendingReceiptRetention;
        var expired = await db.LiveSupportWhatsAppPendingReceipts
            .Where(item => item.CreatedAt < expiresBefore)
            .ToListAsync(ct);
        if (expired.Count == 0) return 0;
        db.LiveSupportWhatsAppPendingReceipts.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
        return expired.Count;
    }

    private async Task IngestAsync(JsonElement envelope, JsonElement message, CancellationToken ct)
    {
        var metaMessageId = Text(message, "id");
        var whatsAppUserId = Text(message, "from");
        if (metaMessageId is null || whatsAppUserId is null || await db.LiveSupportWhatsAppMessages.AnyAsync(item => item.MetaMessageId == metaMessageId, ct)) return;
        var displayName = ContactName(envelope, whatsAppUserId);
        var providerTimestamp = ProviderTimestamp(message);
        var (content, messageType, attachmentId) = await ContentAsync(message, ct);
        var (conversation, participant, binding) = await ConversationAsync(whatsAppUserId, displayName, providerTimestamp, ct);
        binding.DisplayName = displayName;
        AdvanceCustomerServiceWindow(binding, providerTimestamp);
        binding.Version++;
        var clientMessageId = metaMessageId.Length <= 100 ? metaMessageId : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metaMessageId))).ToLowerInvariant();
        var sent = await support.IngestExternalMessageAsync(new LiveSupportExternalMessage(participant, conversation.Id, clientMessageId, content, messageType, attachmentId), ct);
        db.LiveSupportWhatsAppMessages.Add(new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id,
            LiveSupportMessageId = sent.Message.Id,
            MetaMessageId = metaMessageId,
            Direction = "Inbound",
            MessageType = Text(message, "type") ?? "unknown",
            Status = "Received",
            ProviderTimestamp = providerTimestamp,
            Version = 1
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<(LiveSupportConversation Conversation, LiveSupportParticipantIdentity Participant, LiveSupportWhatsAppBinding Binding)> ConversationAsync(
        string whatsAppUserId,
        string displayName,
        DateTime providerTimestamp,
        CancellationToken ct)
    {
        var openConversationIds = await db.LiveSupportConversations.AsNoTracking()
            .Where(item => item.Status != LiveSupportConversationStatus.Closed && item.Status != LiveSupportConversationStatus.Abandoned)
            .Select(item => item.Id).ToListAsync(ct);
        var binding = await db.LiveSupportWhatsAppBindings
            .Where(item => item.WhatsAppUserId == whatsAppUserId && openConversationIds.Contains(item.ConversationId))
            .OrderByDescending(item => item.LastInboundAt).FirstOrDefaultAsync(ct);
        if (binding is not null)
        {
            var existing = await db.LiveSupportConversations.SingleAsync(item => item.Id == binding.ConversationId, ct);
            return (existing, new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, binding.GuestSessionId), binding);
        }

        var phone = NormalizePhone(whatsAppUserId);
        var guest = await db.LiveSupportGuestSessions
            .Where(item => item.PhoneNumber == phone && item.RevokedAt == null)
            .OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(ct);
        if (guest is null)
        {
            guest = new LiveSupportGuestSession
            {
                DisplayName = displayName,
                PhoneNumber = phone,
                SecurityStampHash = RandomHash(),
                CreatedIpHash = Hash("whatsapp-cloud"),
                ExpiresAt = DateTime.UtcNow.AddYears(10),
                LastSeenAt = DateTime.UtcNow,
                UserAgentSummary = "WhatsApp Cloud API"
            };
            db.LiveSupportGuestSessions.Add(guest);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            guest.DisplayName = displayName;
            guest.LastSeenAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guest.Id);
        var conversation = await db.LiveSupportConversations
            .Where(item => item.GuestSessionId == guest.Id && item.Status != LiveSupportConversationStatus.Closed && item.Status != LiveSupportConversationStatus.Abandoned)
            .OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(ct);
        conversation ??= await CreateConversationAsync(participant, ct);
        var linkedStudentId = await FindStudentAsync(phone, ct);
        if (linkedStudentId.HasValue) conversation.LinkedStudentUserId = linkedStudentId;
        binding = new LiveSupportWhatsAppBinding
        {
            ConversationId = conversation.Id,
            GuestSessionId = guest.Id,
            WhatsAppUserId = whatsAppUserId,
            PhoneNumber = phone,
            DisplayName = displayName,
            LastInboundAt = providerTimestamp,
            CustomerServiceWindowExpiresAt = providerTimestamp.AddHours(24),
            Version = 1
        };
        db.LiveSupportWhatsAppBindings.Add(binding);
        await db.SaveChangesAsync(ct);
        return (conversation, participant, binding);
    }

    private async Task<LiveSupportConversation> CreateConversationAsync(LiveSupportParticipantIdentity participant, CancellationToken ct)
    {
        try
        {
            var created = await support.CreateConversationAsync(participant, null, null, ct);
            return await db.LiveSupportConversations.SingleAsync(item => item.Id == created.Id, ct);
        }
        catch (LiveSupportException exception) when (exception.Code == LiveSupportErrorCodes.SupportUnavailable)
        {
            var now = DateTime.UtcNow;
            var conversation = new LiveSupportConversation
            {
                ParticipantType = LiveSupportParticipantType.Guest,
                GuestSessionId = participant.GuestSessionId,
                Status = LiveSupportConversationStatus.Waiting,
                QueuedAt = now,
                LastMessageAt = now,
                Version = 1
            };
            db.LiveSupportConversations.Add(conversation);
            db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversation.Id, EnteredAt = now, Sequence = now.Ticks });
            await db.SaveChangesAsync(ct);
            return conversation;
        }
    }

    private async Task<(string Content, LiveSupportMessageType Type, Guid? AttachmentId)> ContentAsync(JsonElement message, CancellationToken ct)
    {
        var type = Text(message, "type") ?? "unknown";
        if (type == "text" && message.TryGetProperty("text", out var text)) return (Text(text, "body") ?? "رسالة واتساب", LiveSupportMessageType.Text, null);
        if (type == "interactive" && message.TryGetProperty("interactive", out var interactive))
        {
            var reply = interactive.TryGetProperty("button_reply", out var button) ? button : interactive.TryGetProperty("list_reply", out var list) ? list : default;
            return (reply.ValueKind == JsonValueKind.Object ? Text(reply, "title") ?? "تفاعل واتساب" : "تفاعل واتساب", LiveSupportMessageType.Text, null);
        }
        if (type is "image" or "audio" or "document" && message.TryGetProperty(type, out var media) && Text(media, "id") is { } mediaId)
        {
            WhatsAppCloudService.DownloadedMedia downloaded;
            try
            {
                downloaded = await cloud.DownloadMediaAsync(mediaId, ct);
            }
            catch (WhatsAppCloudService.WhatsAppCloudException exception) when (!exception.IsRetryable)
            {
                return (UnavailableMediaMessage, LiveSupportMessageType.Text, null);
            }
            if (type == "document" && !string.Equals(downloaded.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                return (UnsupportedDocumentMessage, LiveSupportMessageType.Text, null);
            await using var content = new MemoryStream(downloaded.Content, writable: false);
            var stored = await attachmentStorage.SaveAsync(content, downloaded.FileName, downloaded.ContentType, downloaded.Content.LongLength, ct);
            var attachment = new LiveSupportAttachment { StoragePath = stored.StoragePath, OriginalFileName = stored.OriginalFileName, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, UploadedByIdentity = "whatsapp" };
            db.LiveSupportAttachments.Add(attachment);
            await db.SaveChangesAsync(ct);
            var supportType = type == "image" ? LiveSupportMessageType.Image : type == "audio" ? LiveSupportMessageType.Audio : LiveSupportMessageType.Pdf;
            return (Text(media, "caption") ?? stored.OriginalFileName, supportType, attachment.Id);
        }
        return ($"رسالة واتساب من النوع: {type}", LiveSupportMessageType.Text, null);
    }

    private async Task ApplyStatusAsync(JsonElement status, CancellationToken ct)
    {
        if (Text(status, "id") is not { } metaMessageId) return;
        var delivery = await db.LiveSupportWhatsAppMessages.SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        var state = Text(status, "status")?.ToLowerInvariant();
        var at = long.TryParse(Text(status, "timestamp"), out var unixTime)
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime
            : delivery?.ProviderTimestamp ?? DateTime.UtcNow;
        var receipt = new ReceiptObservation(metaMessageId, state, at, ReceiptFailureCode(status));
        if (delivery is null)
        {
            await StorePendingReceiptAsync(receipt, ct);
            await ReconcilePendingReceiptAsync(metaMessageId, ct);
            return;
        }
        var pending = await db.LiveSupportWhatsAppPendingReceipts
            .SingleOrDefaultAsync(item => item.MetaMessageId == metaMessageId, ct);
        var changed = pending is not null && ApplyPendingReceipt(delivery, pending);
        if (pending is not null) db.LiveSupportWhatsAppPendingReceipts.Remove(pending);
        changed = ApplyReceipt(delivery, receipt.Status, receipt.ProviderTimestamp, receipt.FailureCode) || changed;
        if (!changed)
        {
            if (pending is not null) await db.SaveChangesAsync(ct);
            return;
        }

        await UpdateCanonicalMessageAsync(delivery, ct);
        await AppendDeliveryEventAsync(delivery, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task StorePendingReceiptAsync(ReceiptObservation receipt, CancellationToken ct)
    {
        var pending = await db.LiveSupportWhatsAppPendingReceipts
            .SingleOrDefaultAsync(item => item.MetaMessageId == receipt.MetaMessageId, ct);
        var accumulated = PendingReceiptAccumulator(pending);
        if (!ApplyReceipt(accumulated, receipt.Status, receipt.ProviderTimestamp, receipt.FailureCode)) return;
        var isNew = pending is null;
        pending ??= new LiveSupportWhatsAppPendingReceipt { MetaMessageId = receipt.MetaMessageId };
        CopyReceiptState(accumulated, pending);
        if (isNew) db.LiveSupportWhatsAppPendingReceipts.Add(pending);
        await db.SaveChangesAsync(ct);
    }

    private async Task UpdateCanonicalMessageAsync(LiveSupportWhatsAppMessage delivery, CancellationToken ct)
    {
        if (delivery.LiveSupportMessageId.HasValue)
        {
            var supportMessage = await db.LiveSupportMessages.SingleOrDefaultAsync(item => item.Id == delivery.LiveSupportMessageId, ct);
            if (supportMessage is not null)
            {
                supportMessage.DeliveredAt = Earlier(supportMessage.DeliveredAt, delivery.DeliveredAt);
                supportMessage.ReadAt = Earlier(supportMessage.ReadAt, delivery.ReadAt);
            }
        }
    }

    private Task AppendDeliveryEventAsync(LiveSupportWhatsAppMessage delivery, CancellationToken ct) =>
        eventWriter.AppendAsync(new LiveSupportEventWriteRequest(
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

    private static LiveSupportWhatsAppMessage PendingReceiptAccumulator(
        LiveSupportWhatsAppPendingReceipt? pending) => new()
    {
        Status = pending?.Status ?? "Pending",
        ProviderTimestamp = pending?.ProviderTimestamp,
        DeliveredAt = pending?.DeliveredAt,
        ReadAt = pending?.ReadAt,
        FailureCode = pending?.FailureCode,
        Version = pending?.Version ?? 0
    };

    private static void CopyReceiptState(
        LiveSupportWhatsAppMessage source,
        LiveSupportWhatsAppPendingReceipt target)
    {
        target.Status = source.Status;
        target.ProviderTimestamp = source.ProviderTimestamp ?? DateTime.UtcNow;
        target.DeliveredAt = source.DeliveredAt;
        target.ReadAt = source.ReadAt;
        target.FailureCode = source.FailureCode;
        target.UpdatedAt = source.UpdatedAt;
        target.Version = source.Version;
    }

    private static bool ApplyPendingReceipt(
        LiveSupportWhatsAppMessage delivery,
        LiveSupportWhatsAppPendingReceipt pending)
    {
        var changed = ApplyReceipt(delivery, pending.Status.ToLowerInvariant(), pending.ProviderTimestamp, pending.FailureCode);
        var deliveredAt = Earlier(delivery.DeliveredAt, pending.DeliveredAt);
        var readAt = Earlier(delivery.ReadAt, pending.ReadAt);
        if (deliveredAt == delivery.DeliveredAt && readAt == delivery.ReadAt) return changed;
        delivery.DeliveredAt = deliveredAt;
        delivery.ReadAt = readAt;
        if (!changed)
        {
            delivery.UpdatedAt = DateTime.UtcNow;
            delivery.Version++;
        }
        return true;
    }

    private sealed record ReceiptObservation(
        string MetaMessageId,
        string? Status,
        DateTime ProviderTimestamp,
        string? FailureCode);

    private static bool ApplyReceipt(
        LiveSupportWhatsAppMessage delivery,
        string? incomingStatus,
        DateTime providerTimestamp,
        string? failureCode)
    {
        var before = (delivery.Status, delivery.ProviderTimestamp, delivery.DeliveredAt,
            delivery.ReadAt, delivery.FailureCode);
        switch (incomingStatus)
        {
            case "sent":
                if (SuccessRank(delivery.Status) > SuccessRank("Sent")) return false;
                delivery.Status = "Sent";
                delivery.FailureCode = null;
                delivery.ProviderTimestamp = Later(delivery.ProviderTimestamp, providerTimestamp);
                break;
            case "delivered":
                delivery.Status = SuccessRank(delivery.Status) > SuccessRank("Delivered")
                    ? delivery.Status
                    : "Delivered";
                delivery.FailureCode = null;
                delivery.ProviderTimestamp = Later(delivery.ProviderTimestamp, providerTimestamp);
                delivery.DeliveredAt = Earlier(delivery.DeliveredAt, providerTimestamp);
                break;
            case "read":
                delivery.Status = "Read";
                delivery.FailureCode = null;
                delivery.ProviderTimestamp = Later(delivery.ProviderTimestamp, providerTimestamp);
                delivery.DeliveredAt = Earlier(delivery.DeliveredAt, providerTimestamp);
                delivery.ReadAt = Earlier(delivery.ReadAt, providerTimestamp);
                break;
            case "failed":
                if (SuccessRank(delivery.Status) >= SuccessRank("Delivered")) return false;
                delivery.Status = "Failed";
                delivery.FailureCode = failureCode ?? "WHATSAPP_DELIVERY_FAILED";
                delivery.ProviderTimestamp = Later(delivery.ProviderTimestamp, providerTimestamp);
                break;
            default:
                return false;
        }

        var after = (delivery.Status, delivery.ProviderTimestamp, delivery.DeliveredAt,
            delivery.ReadAt, delivery.FailureCode);
        if (before == after) return false;
        delivery.UpdatedAt = DateTime.UtcNow;
        delivery.Version++;
        return true;
    }

    private static int SuccessRank(string? status) => status switch
    {
        "Sent" => 1,
        "Delivered" => 2,
        "Read" => 3,
        _ => 0
    };

    private static DateTime Later(DateTime? current, DateTime candidate) =>
        !current.HasValue || candidate > current.Value ? candidate : current.Value;

    private static DateTime? Earlier(DateTime? current, DateTime? candidate)
    {
        if (!candidate.HasValue) return current;
        return !current.HasValue || candidate.Value < current.Value ? candidate : current;
    }

    private static string? ReceiptFailureCode(JsonElement status)
    {
        var error = Array(status, "errors").FirstOrDefault();
        if (error.ValueKind != JsonValueKind.Object || !error.TryGetProperty("code", out var code)) return null;
        var value = code.ValueKind switch
        {
            JsonValueKind.String => code.GetString(),
            JsonValueKind.Number => code.GetRawText(),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(value)) return null;
        var safe = new string(value.Where(character => char.IsAsciiLetterOrDigit(character) || character == '_').Take(80).ToArray());
        return safe.Length == 0 ? null : $"WHATSAPP_CLOUD_{safe}";
    }

    private async Task<Guid?> FindStudentAsync(string phone, CancellationToken ct)
    {
        var international = phone.StartsWith("01", StringComparison.Ordinal) ? $"20{phone[1..]}" : phone;
        return await db.Users.AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted &&
                (item.PhoneNumber == phone || item.PhoneNumber == international) &&
                item.UserRoles.Any(link => link.Role.Type == RoleType.Student))
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static IReadOnlyList<JsonElement> Array(JsonElement value, string property) =>
        value.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(item => item.Clone()).ToArray()
            : [];
    private static string? Text(JsonElement value, string property) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;
    private static string ContactName(JsonElement envelope, string fallback) => Array(envelope, "contacts").FirstOrDefault() is var contact && contact.ValueKind == JsonValueKind.Object && contact.TryGetProperty("profile", out var profile) ? Text(profile, "name") ?? fallback : fallback;
    private static string NormalizePhone(string phone) => phone.StartsWith("20", StringComparison.Ordinal) && phone.Length == 12 ? $"0{phone[2..]}" : phone;
    private static string RandomHash() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private bool MatchesBusinessAccount(JsonElement entry)
    {
        var expected = configuration["WhatsAppCloudApi:BusinessAccountId"];
        return !string.IsNullOrWhiteSpace(expected) &&
            string.Equals(Text(entry, "id"), expected, StringComparison.Ordinal);
    }

    private bool MatchesPhoneNumber(JsonElement value)
    {
        var expected = configuration["WhatsAppCloudApi:PhoneNumberId"];
        return !string.IsNullOrWhiteSpace(expected) &&
            value.TryGetProperty("metadata", out var metadata) &&
            string.Equals(Text(metadata, "phone_number_id"), expected, StringComparison.Ordinal);
    }

    private static DateTime ProviderTimestamp(JsonElement message) =>
        long.TryParse(Text(message, "timestamp"), out var unixTime)
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime
            : DateTime.UtcNow;

    private static void AdvanceCustomerServiceWindow(LiveSupportWhatsAppBinding binding, DateTime inboundAt)
    {
        if (inboundAt > binding.LastInboundAt) binding.LastInboundAt = inboundAt;
        var expiresAt = inboundAt.AddHours(24);
        if (expiresAt > binding.CustomerServiceWindowExpiresAt)
            binding.CustomerServiceWindowExpiresAt = expiresAt;
    }

    private static LiveSupportWhatsAppTemplateDto ToTemplateDto(LiveSupportWhatsAppTemplate template)
    {
        using var document = JsonDocument.Parse(template.ComponentsJson);
        return new(template.Id, template.Name, template.Language, template.Category, template.Status,
            document.RootElement.Clone(), template.LastSyncedAt);
    }
}
