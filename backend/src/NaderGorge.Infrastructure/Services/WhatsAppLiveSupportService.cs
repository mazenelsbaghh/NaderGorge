using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
    IConfiguration configuration,
    IWhatsAppCampaignService campaigns,
    IServiceScopeFactory? serviceScopeFactory = null)
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

    public Task<IReadOnlyList<LiveSupportWhatsAppTemplateDto>> SyncTemplatesAsync(CancellationToken ct) =>
        SyncTemplatesAsync(null, ct);

    public async Task<IReadOnlyList<LiveSupportWhatsAppTemplateDto>> SyncTemplatesAsync(
        Guid? requestedByUserId,
        CancellationToken ct)
    {
        var run = await StartTemplateSyncRunAsync(requestedByUserId, ct);
        try
        {
            var snapshots = await cloud.GetTemplatesAsync(ct);
            if (snapshots.GroupBy(snapshot => (snapshot.Name, snapshot.Language))
                .Any(group => group.Select(snapshot => snapshot.Id).Distinct(StringComparer.Ordinal).Count() > 1))
                throw new InvalidDataException(
                    "WhatsApp returned more than one template identity for the same name and language.");

            var now = DateTime.UtcNow;
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var existingTemplates = await db.LiveSupportWhatsAppTemplates.ToListAsync(ct);
            var templatesByMetaId = existingTemplates.ToDictionary(
                template => template.MetaTemplateId, StringComparer.Ordinal);
            var incomingMetaIds = snapshots.Select(snapshot => snapshot.Id)
                .ToHashSet(StringComparer.Ordinal);
            var rebindableByNameLanguage = existingTemplates
                .Where(template => !incomingMetaIds.Contains(template.MetaTemplateId))
                .GroupBy(template => (template.Name, template.Language))
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id).First());
            var seenTemplateIds = new HashSet<Guid>();
            var changedTemplateIds = new HashSet<Guid>();
            foreach (var snapshot in snapshots)
            {
                var componentsJson = snapshot.Components.GetRawText();
                var fingerprint = WhatsAppCampaignTemplatePolicy.Fingerprint(
                    snapshot.Id, snapshot.Name, snapshot.Language, snapshot.Category,
                    snapshot.Status, componentsJson);
                if (!templatesByMetaId.TryGetValue(snapshot.Id, out var template))
                {
                    if (rebindableByNameLanguage.TryGetValue(
                            (snapshot.Name, snapshot.Language), out var rebound))
                    {
                        template = rebound;
                        templatesByMetaId.Remove(template.MetaTemplateId);
                        template.MetaTemplateId = snapshot.Id;
                        templatesByMetaId[snapshot.Id] = template;
                        template.Version++;
                        run.UpdatedCount++;
                        changedTemplateIds.Add(template.Id);
                    }
                    else
                    {
                        template = new LiveSupportWhatsAppTemplate
                        {
                            MetaTemplateId = snapshot.Id,
                            Version = 1
                        };
                        db.LiveSupportWhatsAppTemplates.Add(template);
                        templatesByMetaId[snapshot.Id] = template;
                        run.CreatedCount++;
                    }
                }
                else if (!string.Equals(template.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    template.Version++;
                    run.UpdatedCount++;
                    changedTemplateIds.Add(template.Id);
                }
                template.Name = snapshot.Name;
                template.Language = snapshot.Language;
                template.Category = snapshot.Category;
                template.Status = snapshot.Status;
                template.ComponentsJson = componentsJson;
                template.Fingerprint = fingerprint;
                template.LastSyncedAt = now;
                template.UpdatedAt = now;
                seenTemplateIds.Add(template.Id);
            }
            foreach (var staleTemplate in existingTemplates.Where(template =>
                         !seenTemplateIds.Contains(template.Id)))
            {
                var previousFingerprint = staleTemplate.Fingerprint;
                var becameStale = !string.Equals(staleTemplate.Status, "STALE", StringComparison.Ordinal);
                staleTemplate.Status = "STALE";
                staleTemplate.LastSyncedAt = now;
                staleTemplate.UpdatedAt = now;
                staleTemplate.Fingerprint = WhatsAppCampaignTemplatePolicy.Fingerprint(staleTemplate);
                if (becameStale || !string.Equals(
                        previousFingerprint, staleTemplate.Fingerprint, StringComparison.Ordinal))
                {
                    staleTemplate.Version++;
                    run.StaleCount++;
                    changedTemplateIds.Add(staleTemplate.Id);
                }
            }
            await db.SaveChangesAsync(ct);
            if (changedTemplateIds.Count > 0 && await db.WhatsAppCampaigns.AsNoTracking().AnyAsync(
                    campaign => campaign.Status == WhatsAppCampaignStatus.Running &&
                        changedTemplateIds.Contains(campaign.TemplateId), ct))
            {
                var pauseReason = "تغير قالب واتساب بعد المراجعة؛ يلزم إنشاء مراجعة جديدة.";
                await db.WhatsAppCampaigns.Where(campaign =>
                        campaign.Status == WhatsAppCampaignStatus.Running &&
                        changedTemplateIds.Contains(campaign.TemplateId))
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(campaign => campaign.Status, WhatsAppCampaignStatus.Paused)
                        .SetProperty(campaign => campaign.PausedAt, now)
                        .SetProperty(campaign => campaign.PauseReason, pauseReason)
                        .SetProperty(campaign => campaign.UpdatedAt, now)
                        .SetProperty(campaign => campaign.Version, campaign => campaign.Version + 1), ct);
            }
            run.ReceivedCount = snapshots.Count;
            run.Status = WhatsAppTemplateSyncRunStatus.Succeeded;
            run.CompletedAt = DateTime.UtcNow;
            run.UpdatedAt = run.CompletedAt;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await ListTemplatesAsync(ct);
        }
        catch (Exception exception)
        {
            await FinalizeTemplateSyncFailureAsync(run.Id, SyncFailureCode(exception));
            throw;
        }
    }

    private async Task<WhatsAppTemplateSyncRun> StartTemplateSyncRunAsync(
        Guid? requestedByUserId,
        CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-30);
        var staleRuns = await db.WhatsAppTemplateSyncRuns
            .Where(item => item.Status == WhatsAppTemplateSyncRunStatus.Running &&
                item.StartedAt < staleBefore)
            .ToListAsync(ct);
        foreach (var staleRun in staleRuns)
        {
            staleRun.Status = WhatsAppTemplateSyncRunStatus.Failed;
            staleRun.FailureCode = "WHATSAPP_TEMPLATE_SYNC_STALE";
            staleRun.CompletedAt = now;
            staleRun.UpdatedAt = now;
        }
        if (staleRuns.Count > 0) await db.SaveChangesAsync(ct);
        if (await db.WhatsAppTemplateSyncRuns.AsNoTracking().AnyAsync(item =>
                item.Status == WhatsAppTemplateSyncRunStatus.Running, ct))
            throw new WhatsAppCampaignException(
                WhatsAppCampaignErrorCodes.Conflict,
                "مزامنة قوالب واتساب قيد التنفيذ بالفعل.", 409);
        var run = new WhatsAppTemplateSyncRun
        {
            RequestedByUserId = requestedByUserId,
            Status = WhatsAppTemplateSyncRunStatus.Running,
            StartedAt = now
        };
        db.WhatsAppTemplateSyncRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return run;
        }
        catch (DbUpdateException exception) when (IsTemplateSyncSingleFlightConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.Entry(run).State = EntityState.Detached;
            throw new WhatsAppCampaignException(
                WhatsAppCampaignErrorCodes.Conflict,
                "مزامنة قوالب واتساب قيد التنفيذ بالفعل.", 409);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.Entry(run).State = EntityState.Detached;
            throw new WhatsAppCampaignException(
                WhatsAppCampaignErrorCodes.Conflict,
                "مزامنة قوالب واتساب قيد التنفيذ بالفعل.", 409);
        }
    }

    private async Task FinalizeTemplateSyncFailureAsync(Guid runId, string failureCode)
    {
        try
        {
            if (serviceScopeFactory is not null)
            {
                using var scope = serviceScopeFactory.CreateScope();
                var freshDb = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                await MarkTemplateSyncFailedAsync(freshDb, runId, failureCode);
                return;
            }
            await MarkTemplateSyncFailedAsync(db, runId, failureCode);
        }
        catch
        {
            // The original sync error remains authoritative. A stale Running row is
            // recovered by StartTemplateSyncRunAsync after the bounded timeout.
        }
    }

    private static Task<int> MarkTemplateSyncFailedAsync(
        IAppDbContext targetDb,
        Guid runId,
        string failureCode)
    {
        var now = DateTime.UtcNow;
        return targetDb.WhatsAppTemplateSyncRuns.Where(item =>
                item.Id == runId && item.Status == WhatsAppTemplateSyncRunStatus.Running)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, WhatsAppTemplateSyncRunStatus.Failed)
                .SetProperty(item => item.FailureCode, failureCode)
                .SetProperty(item => item.CompletedAt, now)
                .SetProperty(item => item.UpdatedAt, now), CancellationToken.None);
    }

    private static bool IsTemplateSyncSingleFlightConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure
        };

    private static string SyncFailureCode(Exception exception) => exception switch
    {
        WhatsAppCloudService.WhatsAppCloudException cloudException => cloudException.ErrorCode,
        OperationCanceledException => "WHATSAPP_TEMPLATE_SYNC_CANCELLED",
        InvalidDataException => "WHATSAPP_TEMPLATE_SYNC_INVALID_RESPONSE",
        DbUpdateException => "WHATSAPP_TEMPLATE_SYNC_PERSISTENCE_FAILED",
        _ => "WHATSAPP_TEMPLATE_SYNC_FAILED"
    };

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
        if (metaMessageId is null || whatsAppUserId is null) return;
        var providerTimestamp = ProviderTimestamp(message);
        if (IsOptOutKeyword(message))
            await campaigns.RecordInboundOptOutAsync(whatsAppUserId, metaMessageId, providerTimestamp, ct);
        if (await db.LiveSupportWhatsAppMessages.AnyAsync(item => item.MetaMessageId == metaMessageId, ct)) return;
        var displayName = ContactName(envelope, whatsAppUserId);
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
        var campaignHandled = await campaigns.ProcessReceiptAsync(
            receipt.MetaMessageId, receipt.Status, receipt.ProviderTimestamp, receipt.FailureCode, ct);
        if (delivery is null)
        {
            if (campaignHandled) return;
            await StorePendingReceiptAsync(receipt, ct);
            await ReconcilePendingReceiptAsync(metaMessageId, ct);
            await campaigns.ReconcilePendingReceiptAsync(metaMessageId, ct);
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
                if (IsOlderTerminalObservation(delivery, providerTimestamp) ||
                    delivery.ProviderTimestamp == providerTimestamp && delivery.Status == "Failed")
                    return false;
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
                if (IsOlderTerminalObservation(delivery, providerTimestamp)) return false;
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

    private static bool IsOlderTerminalObservation(
        LiveSupportWhatsAppMessage delivery,
        DateTime providerTimestamp) =>
        delivery.Status is "Sent" or "Failed" &&
        delivery.ProviderTimestamp.HasValue &&
        providerTimestamp < delivery.ProviderTimestamp.Value;

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

    private static bool IsOptOutKeyword(JsonElement message)
    {
        if (!string.Equals(Text(message, "type"), "text", StringComparison.OrdinalIgnoreCase) ||
            !message.TryGetProperty("text", out var text)) return false;
        var body = Text(text, "body")?.Normalize(NormalizationForm.FormC).Trim();
        return string.Equals(body, "STOP", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(body, "إيقاف", StringComparison.Ordinal) ||
            string.Equals(body, "ايقاف", StringComparison.Ordinal);
    }

    private async Task<Guid?> FindStudentAsync(string phone, CancellationToken ct)
    {
        var normalized = WhatsAppCampaignService.NormalizeE164(phone);
        if (normalized is null) return null;
        var suffix = normalized[^8..];
        var candidates = await db.Users.AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted &&
                item.StudentProfile != null &&
                item.UserRoles.Any(link => link.Role.Type == RoleType.Student) &&
                (item.PhoneNumber.EndsWith(suffix) ||
                 item.StudentProfile.SecondaryPhone != null &&
                 item.StudentProfile.SecondaryPhone.EndsWith(suffix) ||
                 item.StudentProfile.ParentPhone != null &&
                 item.StudentProfile.ParentPhone.EndsWith(suffix) ||
                 item.StudentProfile.SecondaryParentPhone != null &&
                 item.StudentProfile.SecondaryParentPhone.EndsWith(suffix) ||
                 item.StudentProfile.MotherPhone != null &&
                 item.StudentProfile.MotherPhone.EndsWith(suffix)))
            .OrderBy(item => item.Id)
            .Take(101)
            .Select(item => new
            {
                item.Id,
                Primary = item.PhoneNumber,
                Secondary = item.StudentProfile!.SecondaryPhone,
                Father = item.StudentProfile.ParentPhone,
                FatherSecondary = item.StudentProfile.SecondaryParentPhone,
                Mother = item.StudentProfile.MotherPhone
            })
            .ToListAsync(ct);
        if (candidates.Count > 100) return null;
        var matches = candidates.Where(candidate =>
                new[] { candidate.Primary, candidate.Secondary, candidate.Father,
                        candidate.FatherSecondary, candidate.Mother }
                    .Any(value => string.Equals(
                        WhatsAppCampaignService.NormalizeE164(value), normalized, StringComparison.Ordinal)))
            .Select(candidate => candidate.Id)
            .Distinct()
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
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
            document.RootElement.Clone(), template.LastSyncedAt, template.Version, template.Fingerprint);
    }
}
