using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class WhatsAppLiveSupportTests
{
    private static readonly string ValidTemplateFingerprint = new('a', 64);

    [Fact]
    public async Task DuplicateWebhookDelivery_CreatesOneSupportMessage()
    {
        await using var db = TestAppDbContextFactory.Create();
        var guest = LiveSupportTestData.Guest();
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        db.LiveSupportWhatsAppBindings.Add(new LiveSupportWhatsAppBinding
        {
            ConversationId = conversation.Id,
            GuestSessionId = guest.Id,
            WhatsAppUserId = "201099999999",
            PhoneNumber = guest.PhoneNumber,
            DisplayName = guest.DisplayName,
            LastInboundAt = DateTime.UtcNow,
            CustomerServiceWindowExpiresAt = DateTime.UtcNow.AddHours(24),
            Version = 1
        });
        await db.SaveChangesAsync();

        var support = new LiveSupportService(db, new LiveSupportEnabledSettings());
        var cloud = new WhatsAppCloudService(new HttpClient(), new ConfigurationBuilder().Build(), NullLogger<WhatsAppCloudService>.Instance);
        var service = new WhatsAppLiveSupportService(
            db, support, new RejectingAttachmentStorage(), cloud, new LiveSupportEventWriter(db), ChannelConfiguration(),
            new StubWhatsAppCampaignService());
        using var webhook = JsonDocument.Parse(WebhookJson);

        await service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);
        await service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);

        Assert.Single(db.LiveSupportMessages);
        Assert.Single(db.LiveSupportWhatsAppMessages);
        Assert.Equal("رسالة اختبار", db.LiveSupportMessages.Single().Content);
    }

    [Theory]
    [InlineData("other-business", "phone-id")]
    [InlineData("business-id", "other-phone")]
    public async Task WebhookForDifferentConfiguredChannel_IsIgnored(string businessAccountId, string phoneNumberId)
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedConversationAsync(db);
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));
        using var webhook = JsonDocument.Parse(ChannelWebhookJson(
            "wamid.foreign",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            businessAccountId,
            phoneNumberId));

        await service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);

        Assert.Empty(db.LiveSupportMessages);
        Assert.Empty(db.LiveSupportWhatsAppMessages);
    }

    [Fact]
    public async Task NewWhatsAppContact_DoesNotLinkNonStudentAccountWithSamePhone()
    {
        await using var db = TestAppDbContextFactory.Create();
        var staff = LiveSupportTestData.User(Guid.NewGuid(), "موظف", "01099999999");
        db.Users.Add(staff);
        await db.SaveChangesAsync();
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));
        using var webhook = JsonDocument.Parse(WebhookJson);

        await service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);

        var conversation = Assert.Single(db.LiveSupportConversations);
        Assert.Null(conversation.LinkedStudentUserId);
        Assert.Single(db.LiveSupportMessages);
    }

    [Fact]
    public async Task OutOfOrderInboundMessages_DoNotRegressCustomerServiceWindow()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (_, conversation) = await SeedConversationAsync(db);
        var binding = await db.LiveSupportWhatsAppBindings.SingleAsync(item => item.ConversationId == conversation.Id);
        binding.LastInboundAt = DateTime.UtcNow.AddHours(-4);
        binding.CustomerServiceWindowExpiresAt = binding.LastInboundAt.AddHours(24);
        await db.SaveChangesAsync();
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));
        var newestAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var olderAt = newestAt - 3600;

        using (var newest = JsonDocument.Parse(ChannelWebhookJson("wamid.newest", newestAt)))
            await service.ProcessWebhookAsync(newest.RootElement, CancellationToken.None);
        using (var older = JsonDocument.Parse(ChannelWebhookJson("wamid.older", olderAt)))
            await service.ProcessWebhookAsync(older.RootElement, CancellationToken.None);

        await db.Entry(binding).ReloadAsync();
        var expectedInbound = DateTimeOffset.FromUnixTimeSeconds(newestAt).UtcDateTime;
        Assert.Equal(expectedInbound, binding.LastInboundAt);
        Assert.Equal(expectedInbound.AddHours(24), binding.CustomerServiceWindowExpiresAt);
        Assert.Equal(2, db.LiveSupportMessages.Count());
    }

    [Fact]
    public async Task AudioSend_UploadsOggThenSendsVoiceWithoutCaption()
    {
        var handler = new RecordingMetaHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsAppCloudApi:AccessToken"] = "test-token",
            ["WhatsAppCloudApi:PhoneNumberId"] = "phone-id"
        }).Build();
        var cloud = new WhatsAppCloudService(new HttpClient(handler), configuration, NullLogger<WhatsAppCloudService>.Instance);

        var response = await cloud.SendMediaAsync(new WhatsAppCloudService.MediaMessageRequest(
            "01099999999", "audio", "reply.ogg", "audio/ogg", [1, 2, 3], "رد صوتي"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("wamid.sent", response.MetaMessageId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/phone-id/media", handler.Requests[0].Url);
        Assert.Contains("Content-Type: audio/ogg", handler.Requests[0].Body);
        Assert.Contains("reply.ogg", handler.Requests[0].Body);
        Assert.Contains("media-1", handler.Requests[1].Body);
        Assert.Contains("\"type\":\"audio\"", handler.Requests[1].Body);
        Assert.Contains("\"voice\":true", handler.Requests[1].Body);
        Assert.DoesNotContain("caption", handler.Requests[1].Body);
    }

    [Fact]
    public async Task ImageSend_AllowsCaptionWithoutVoiceFlag()
    {
        var handler = new RecordingMetaHandler();
        var cloud = Cloud(handler);

        var response = await cloud.SendMediaAsync(new WhatsAppCloudService.MediaMessageRequest(
            "01099999999", "image", "reply.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF], "صورة"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("Content-Type: image/jpeg", handler.Requests[0].Body);
        Assert.Contains("reply.jpg", handler.Requests[0].Body);
        using var sentMessage = JsonDocument.Parse(handler.Requests[1].Body);
        var imagePayload = sentMessage.RootElement.GetProperty("image");
        Assert.Equal("صورة", imagePayload.GetProperty("caption").GetString());
        Assert.False(imagePayload.TryGetProperty("voice", out _));
    }

    [Theory]
    [InlineData("image", "image/webp", 3, "WHATSAPP_MEDIA_UNSUPPORTED")]
    [InlineData("audio", "audio/webm", 3, "WHATSAPP_MEDIA_UNSUPPORTED")]
    [InlineData("video", "video/mp4", 3, "WHATSAPP_MEDIA_UNSUPPORTED")]
    [InlineData("image", "image/jpeg", 0, "WHATSAPP_MEDIA_EMPTY")]
    [InlineData("image", "image/jpeg", 5 * 1024 * 1024 + 1, "WHATSAPP_MEDIA_TOO_LARGE")]
    [InlineData("audio", "audio/ogg", 16 * 1024 * 1024 + 1, "WHATSAPP_MEDIA_TOO_LARGE")]
    public async Task UnsupportedOrOversizedOutboundMedia_FailsBeforeProviderUpload(
        string mediaType,
        string contentType,
        int contentLength,
        string expectedErrorCode)
    {
        var handler = new RecordingMetaHandler();
        var cloud = Cloud(handler);

        var response = await cloud.SendMediaAsync(new WhatsAppCloudService.MediaMessageRequest(
            "01099999999", mediaType, "stored.bin", contentType, new byte[contentLength], "caption"),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(expectedErrorCode, response.ErrorCode);
        Assert.False(response.IsRetryable);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SuccessfulSendWithoutProviderId_IsReportedAsInvalidResponse()
    {
        var cloud = Cloud(new StubMetaHandler(_ => JsonResponse(HttpStatusCode.OK, "{}")));

        var response = await cloud.SendTextAsync("01099999999", "اختبار", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.BadGateway, (HttpStatusCode)response.StatusCode);
        Assert.Equal("WHATSAPP_CLOUD_INVALID_RESPONSE", response.ErrorCode);
        Assert.Null(response.MetaMessageId);
    }

    [Fact]
    public async Task SuccessfulMediaUploadWithoutProviderId_DoesNotSendMessage()
    {
        var handler = new StubMetaHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var cloud = Cloud(handler);

        var response = await cloud.SendMediaAsync(new WhatsAppCloudService.MediaMessageRequest(
            "01099999999", "image", "reply.jpg", "image/jpeg", [1, 2, 3], null), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("WHATSAPP_CLOUD_INVALID_RESPONSE", response.ErrorCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TemplateSyncFailure_IsPropagatedWithoutChangingStoredTemplates()
    {
        await using var db = TestAppDbContextFactory.Create();
        var template = new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = "template-1",
            Name = "existing",
            Language = "ar",
            Category = "UTILITY",
            Status = "APPROVED",
            ComponentsJson = "[]",
            LastSyncedAt = DateTime.UtcNow.AddDays(-1),
            Version = 3
        };
        db.LiveSupportWhatsAppTemplates.Add(template);
        await db.SaveChangesAsync();
        var cloud = Cloud(new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.ServiceUnavailable,
            "{\"error\":{\"code\":2,\"message\":\"temporarily unavailable\"}}")), includeBusinessAccount: true);
        var service = Service(db, cloud);

        var exception = await Assert.ThrowsAsync<WhatsAppCloudService.WhatsAppCloudException>(
            () => service.SyncTemplatesAsync(CancellationToken.None));

        Assert.True(exception.IsRetryable);
        Assert.Equal(3, template.Version);
        Assert.Equal("APPROVED", template.Status);
        Assert.Single(db.LiveSupportWhatsAppTemplates);
    }

    [Fact]
    public async Task SuccessfulTemplateSync_MarksTemplatesMissingFromMetaAsStale()
    {
        await using var db = TestAppDbContextFactory.Create();
        var previousSyncAt = DateTime.UtcNow.AddDays(-1);
        db.LiveSupportWhatsAppTemplates.AddRange(
            new LiveSupportWhatsAppTemplate
            {
                MetaTemplateId = "template-current",
                Name = "current_template",
                Language = "ar",
                Category = "UTILITY",
                Status = "APPROVED",
                ComponentsJson = "[]",
                LastSyncedAt = previousSyncAt,
                Version = 1
            },
            new LiveSupportWhatsAppTemplate
            {
                MetaTemplateId = "template-removed",
                Name = "removed_template",
                Language = "ar",
                Category = "UTILITY",
                Status = "APPROVED",
                ComponentsJson = "[]",
                LastSyncedAt = previousSyncAt,
                Version = 1
            });
        await db.SaveChangesAsync();
        var cloud = Cloud(new StubMetaHandler(_ => JsonResponse(HttpStatusCode.OK,
            "{\"data\":[{\"id\":\"template-current\",\"name\":\"current_template\",\"language\":\"ar\",\"category\":\"UTILITY\",\"status\":\"APPROVED\",\"components\":[]}]}")), includeBusinessAccount: true);
        var service = Service(db, cloud);

        await service.SyncTemplatesAsync(CancellationToken.None);

        var current = await db.LiveSupportWhatsAppTemplates.SingleAsync(item => item.MetaTemplateId == "template-current");
        var stale = await db.LiveSupportWhatsAppTemplates.SingleAsync(item => item.MetaTemplateId == "template-removed");
        Assert.Equal("APPROVED", current.Status);
        Assert.Equal("STALE", stale.Status);
        Assert.True(stale.LastSyncedAt > previousSyncAt);
        Assert.Equal(2, stale.Version);
    }

    [Fact]
    public async Task TemplateListing_FollowsEveryCursorPage()
    {
        var pageNumber = 0;
        var handler = new StubMetaHandler(_ => ++pageNumber == 1
            ? JsonResponse(HttpStatusCode.OK,
                """{"data":[{"id":"template-1","name":"first","language":"ar","category":"UTILITY","status":"APPROVED","components":[]}],"paging":{"cursors":{"after":"cursor+/="},"next":"https://graph.facebook.com/next"}}""")
            : JsonResponse(HttpStatusCode.OK,
                """{"data":[{"id":"template-2","name":"second","language":"en_US","category":"MARKETING","status":"PAUSED","components":[]}]}"""));
        var cloud = Cloud(handler, includeBusinessAccount: true);

        var templates = await cloud.GetTemplatesAsync(CancellationToken.None);

        Assert.Equal(["template-1", "template-2"], templates.Select(item => item.Id));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("after=cursor+/=", Uri.UnescapeDataString(handler.Requests[1]));
    }

    [Fact]
    public async Task DownloadMedia_StopsReadingAtTheConfiguredLimitWithoutContentLength()
    {
        var media = new CountingReadStream(100L * 1024 * 1024);
        var handler = new StubMetaHandler(request =>
        {
            if (request.RequestUri?.Host == "media.test")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(media)
                };
            }

            return JsonResponse(HttpStatusCode.OK,
                """{"url":"https://media.test/content","mime_type":"image/jpeg"}""");
        });
        var cloud = Cloud(handler);

        var exception = await Assert.ThrowsAsync<WhatsAppCloudService.WhatsAppCloudException>(
            () => cloud.DownloadMediaAsync("media-id", CancellationToken.None));

        Assert.Equal("WHATSAPP_MEDIA_TOO_LARGE", exception.ErrorCode);
        Assert.False(exception.IsRetryable);
        Assert.Equal(10L * 1024 * 1024 + 1, media.BytesRead);
    }

    [Theory]
    [InlineData(400, false, false)]
    [InlineData(400, true, true)]
    [InlineData(429, false, true)]
    [InlineData(503, false, true)]
    public async Task ProviderFailures_AreClassifiedForTextAndTemplateDispatch(
        int statusCode,
        bool isTransient,
        bool expectedRetryable)
    {
        var transient = isTransient.ToString().ToLowerInvariant();
        var body = $"{{\"error\":{{\"code\":100,\"message\":\"provider failure\",\"is_transient\":{transient}}}}}";
        var handler = new StubMetaHandler(_ => JsonResponse((HttpStatusCode)statusCode, body));
        var cloud = Cloud(handler);

        var text = await cloud.SendTextAsync("01099999999", "اختبار", CancellationToken.None);
        var template = await cloud.SendTemplateAsync(new WhatsAppCloudService.TemplateMessageRequest(
            "01099999999", "approved_template", "ar", []), CancellationToken.None);

        Assert.All(new[] { text, template }, response =>
        {
            Assert.False(response.Success);
            Assert.Equal(statusCode, response.StatusCode);
            Assert.Equal(expectedRetryable, response.IsRetryable);
        });
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task CampaignTemplateComponents_SerializeToExactMetaTextAndDynamicUrlShape()
    {
        var handler = new BodyRecordingMetaHandler();
        var cloud = Cloud(handler);

        var response = await cloud.SendTemplateAsync(new WhatsAppCloudService.TemplateMessageRequest(
            "01099999999",
            "progress_template",
            "ar",
            [
                new WhatsAppCloudService.TemplateComponent("BODY", ["Mazen", "95%"]),
                new WhatsAppCloudService.TemplateComponent("BUTTON", ["student-token"], "text", "url", 0)
            ]), CancellationToken.None);

        Assert.True(response.Success);
        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        var components = request.RootElement.GetProperty("template").GetProperty("components");
        var body = components[0];
        Assert.Equal("body", body.GetProperty("type").GetString());
        Assert.False(body.TryGetProperty("sub_type", out _));
        Assert.False(body.TryGetProperty("index", out _));
        Assert.Equal("text", body.GetProperty("parameters")[0].GetProperty("type").GetString());
        Assert.Equal("Mazen", body.GetProperty("parameters")[0].GetProperty("text").GetString());
        Assert.Equal("95%", body.GetProperty("parameters")[1].GetProperty("text").GetString());
        var button = components[1];
        Assert.Equal("button", button.GetProperty("type").GetString());
        Assert.Equal("url", button.GetProperty("sub_type").GetString());
        Assert.Equal("0", button.GetProperty("index").GetString());
        Assert.Equal("text", button.GetProperty("parameters")[0].GetProperty("type").GetString());
        Assert.Equal("student-token", button.GetProperty("parameters")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task StaticTemplate_OmitsEmptyComponentsFromMetaPayload()
    {
        var handler = new BodyRecordingMetaHandler();
        var cloud = Cloud(handler);

        var response = await cloud.SendTemplateAsync(new WhatsAppCloudService.TemplateMessageRequest(
            "01099999999", "hello_world", "en_US", []), CancellationToken.None);

        Assert.True(response.Success);
        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.False(request.RootElement.GetProperty("template").TryGetProperty("components", out _));
    }

    [Fact]
    public async Task InboundMediaProviderFailure_IsPropagatedWithoutPlaceholderMessage()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedConversationAsync(db);
        var cloud = Cloud(new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.ServiceUnavailable,
            "{\"error\":{\"code\":2}}")));
        var service = Service(db, cloud);
        using var webhook = JsonDocument.Parse(MediaWebhookJson);

        await Assert.ThrowsAsync<WhatsAppCloudService.WhatsAppCloudException>(
            () => service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None));

        Assert.Empty(db.LiveSupportMessages);
        Assert.Empty(db.LiveSupportWhatsAppMessages);
    }

    [Fact]
    public async Task PermanentInboundMediaFailure_IsRecordedWithoutRequestRetry()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedConversationAsync(db);
        var cloud = Cloud(new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.RequestEntityTooLarge,
            "{\"error\":{\"code\":131052}}")));
        var service = Service(db, cloud);
        using var webhook = JsonDocument.Parse(MediaWebhookJson);

        await service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);

        var message = Assert.Single(db.LiveSupportMessages);
        Assert.Equal(LiveSupportMessageType.Text, message.Type);
        Assert.Contains("غير متاح أو غير مدعوم", message.Content);
        Assert.Null(message.AttachmentId);
        Assert.Single(db.LiveSupportWhatsAppMessages);
        Assert.Empty(db.LiveSupportAttachments);
    }

    [Fact]
    public async Task NonPdfWhatsAppDocument_IsRecordedAsUnsupportedTextWithoutStorage()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedConversationAsync(db);
        var requests = 0;
        var cloud = Cloud(new StubMetaHandler(_ =>
        {
            requests++;
            return requests == 1
                ? JsonResponse(HttpStatusCode.OK,
                    "{\"url\":\"https://cdn.example/document\",\"mime_type\":\"application/vnd.openxmlformats-officedocument.wordprocessingml.document\"}")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        }));
        var service = Service(db, cloud);
        using var webhook = JsonDocument.Parse(DocumentWebhookJson);

        await service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);

        var message = Assert.Single(db.LiveSupportMessages);
        Assert.Equal(LiveSupportMessageType.Text, message.Type);
        Assert.Contains("PDF", message.Content);
        Assert.Null(message.AttachmentId);
        Assert.Empty(db.LiveSupportAttachments);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task OutOfOrderReceipts_RemainMonotonicAndPublishSafeRealtimeEvents()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (_, conversation) = await SeedConversationAsync(db);
        var supportMessage = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Staff,
            ClientMessageId = "client-1",
            Type = LiveSupportMessageType.Text,
            Content = "رد",
            SentAt = DateTime.UtcNow
        };
        var delivery = new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id,
            LiveSupportMessageId = supportMessage.Id,
            MetaMessageId = "wamid.receipt-1",
            Direction = "Outbound",
            MessageType = "text",
            Status = "Sent",
            AttemptCount = 1,
            Version = 1
        };
        db.LiveSupportMessages.Add(supportMessage);
        db.LiveSupportWhatsAppMessages.Add(delivery);
        await db.SaveChangesAsync();
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));

        await ProcessStatusAsync(service, "read", 300);
        await ProcessStatusAsync(service, "delivered", 200);
        await ProcessStatusAsync(service, "sent", 400);
        await ProcessStatusAsync(service, "failed", 500, 131026);
        await ProcessStatusAsync(service, "delivered", 200);

        Assert.Equal("Read", delivery.Status);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(200).UtcDateTime, delivery.DeliveredAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(300).UtcDateTime, delivery.ReadAt);
        Assert.Equal(delivery.DeliveredAt, supportMessage.DeliveredAt);
        Assert.Equal(delivery.ReadAt, supportMessage.ReadAt);
        Assert.Null(delivery.FailureCode);
        Assert.Equal(2, db.LiveSupportEvents.Count(item =>
            item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
        Assert.Equal(4, db.OutboxEvents.Count(item => item.Type == "LiveSupportEvent"));
        Assert.All(db.OutboxEvents.Where(item => item.Type == "LiveSupportEvent"), item =>
        {
            Assert.Contains("WhatsAppDeliveryStatusChanged", item.PayloadJson);
            Assert.DoesNotContain("wamid.receipt-1", item.PayloadJson);
            Assert.DoesNotContain("01099999999", item.PayloadJson);
        });
    }

    [Fact]
    public async Task ReceiptBeforeDelivery_IsReconciledIntoCanonicalMessageAndRealtimeEvent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (_, conversation) = await SeedConversationAsync(db);
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));

        await ProcessStatusAsync(service, "read", 300);

        var pending = Assert.Single(db.LiveSupportWhatsAppPendingReceipts);
        Assert.Equal("Read", pending.Status);
        Assert.Empty(db.LiveSupportEvents.Where(item => item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
        var supportMessage = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Staff,
            ClientMessageId = Guid.NewGuid().ToString("N"),
            Type = LiveSupportMessageType.Text,
            Content = "رد",
            SentAt = DateTime.UtcNow
        };
        var delivery = new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id,
            LiveSupportMessageId = supportMessage.Id,
            MetaMessageId = "wamid.receipt-1",
            Direction = "Outbound",
            MessageType = "text",
            Status = "Sent",
            ProviderTimestamp = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportMessages.Add(supportMessage);
        db.LiveSupportWhatsAppMessages.Add(delivery);
        await db.SaveChangesAsync();

        var reconciled = await service.ReconcilePendingReceiptAsync("wamid.receipt-1", CancellationToken.None);

        Assert.True(reconciled);
        Assert.Empty(db.LiveSupportWhatsAppPendingReceipts);
        var reconciledDelivery = await db.LiveSupportWhatsAppMessages.AsNoTracking()
            .SingleAsync(item => item.Id == delivery.Id);
        var reconciledMessage = await db.LiveSupportMessages.AsNoTracking()
            .SingleAsync(item => item.Id == supportMessage.Id);
        Assert.Equal("Read", reconciledDelivery.Status);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(300).UtcDateTime, reconciledDelivery.DeliveredAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(300).UtcDateTime, reconciledDelivery.ReadAt);
        Assert.Equal(reconciledDelivery.DeliveredAt, reconciledMessage.DeliveredAt);
        Assert.Equal(reconciledDelivery.ReadAt, reconciledMessage.ReadAt);
        Assert.Single(db.LiveSupportEvents.Where(item => item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
        Assert.Equal(2, db.OutboxEvents.Count(item => item.Type == "LiveSupportEvent"));
    }

    [Fact]
    public async Task IdenticalPendingAndDeliveryStatus_StillPublishesOneReconciliationEvent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (_, conversation) = await SeedConversationAsync(db);
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));
        await ProcessStatusAsync(service, "sent", 100);
        var supportMessage = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Staff,
            ClientMessageId = Guid.NewGuid().ToString("N"),
            Type = LiveSupportMessageType.Text,
            Content = "رد",
            SentAt = DateTime.UtcNow
        };
        db.LiveSupportMessages.Add(supportMessage);
        db.LiveSupportWhatsAppMessages.Add(new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id,
            LiveSupportMessageId = supportMessage.Id,
            MetaMessageId = "wamid.receipt-1",
            Direction = "Outbound",
            MessageType = "text",
            Status = "Sent",
            ProviderTimestamp = DateTimeOffset.FromUnixTimeSeconds(100).UtcDateTime,
            Version = 1
        });
        await db.SaveChangesAsync();

        var reconciled = await service.ReconcilePendingReceiptAsync("wamid.receipt-1", CancellationToken.None);

        Assert.True(reconciled);
        Assert.Empty(db.LiveSupportWhatsAppPendingReceipts);
        Assert.Single(db.LiveSupportEvents.Where(item => item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
        Assert.Equal(2, db.OutboxEvents.Count(item => item.Type == "LiveSupportEvent"));
    }

    [Fact]
    public async Task DuplicateOutOfOrderUnmatchedReceipts_CollapseToOneMonotonicInboxRow()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedConversationAsync(db);
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));

        await ProcessStatusAsync(service, "read", 300);
        await ProcessStatusAsync(service, "delivered", 200);
        await ProcessStatusAsync(service, "sent", 400);
        await ProcessStatusAsync(service, "failed", 500, 131026);
        await ProcessStatusAsync(service, "delivered", 200);

        var pending = Assert.Single(db.LiveSupportWhatsAppPendingReceipts);
        Assert.Equal("Read", pending.Status);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(200).UtcDateTime, pending.DeliveredAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(300).UtcDateTime, pending.ReadAt);
        Assert.Null(pending.FailureCode);
        Assert.Empty(db.LiveSupportEvents.Where(item => item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
        Assert.Empty(db.OutboxEvents.Where(item => item.Type == "LiveSupportEvent"));
    }

    [Fact]
    public async Task PendingReceiptCleanup_RemovesOnlyRowsOlderThanThirtyDays()
    {
        await using var db = TestAppDbContextFactory.Create();
        var now = DateTime.UtcNow;
        db.LiveSupportWhatsAppPendingReceipts.AddRange(
            PendingReceipt("wamid.expired", now.AddDays(-31)),
            PendingReceipt("wamid.fresh", now.AddDays(-29)));
        await db.SaveChangesAsync();
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));

        var removed = await service.CleanupExpiredPendingReceiptsAsync(now, CancellationToken.None);

        Assert.Equal(1, removed);
        var remaining = Assert.Single(db.LiveSupportWhatsAppPendingReceipts);
        Assert.Equal("wamid.fresh", remaining.MetaMessageId);
    }

    [Fact]
    public async Task SuccessfulReceiptAfterFailure_ClearsFailureAndAdvancesDelivery()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (_, conversation) = await SeedConversationAsync(db);
        var delivery = new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id,
            MetaMessageId = "wamid.receipt-1",
            Direction = "Outbound",
            MessageType = "template",
            Status = "Pending",
            Version = 1
        };
        db.LiveSupportWhatsAppMessages.Add(delivery);
        await db.SaveChangesAsync();
        var service = Service(db, Cloud(new StubMetaHandler(_ => throw new InvalidOperationException())));

        await ProcessStatusAsync(service, "failed", 100, 2);
        await ProcessStatusAsync(service, "sent", 110);

        Assert.Equal("Sent", delivery.Status);
        Assert.Null(delivery.FailureCode);
        Assert.Equal(2, db.LiveSupportEvents.Count(item =>
            item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
    }

    [Fact]
    public async Task StaffWhatsAppSend_PersistsCanonicalAndOutboundRowsInOneSave()
    {
        await using var scenario = await CreateStaffWhatsAppScenarioAsync();

        Assert.Equal(1, scenario.Db.SaveCount);
        Assert.Equal("Pending", scenario.SendResult.Message.ExternalDeliveryStatus);
        var message = Assert.Single(scenario.Db.LiveSupportMessages);
        var outbound = Assert.Single(scenario.Db.LiveSupportWhatsAppMessages);
        Assert.Equal(message.Id, outbound.LiveSupportMessageId);
        Assert.Equal("Pending", outbound.Status);
        Assert.NotNull(scenario.Conversation.FirstStaffResponseAt);
        Assert.Equal(LiveSupportConversationStatus.Active, scenario.Conversation.Status);
    }

    [Fact]
    public async Task StaffWhatsAppHistory_EnrichesSenderAndDeliveryStatus()
    {
        await using var scenario = await CreateStaffWhatsAppScenarioAsync();

        var loadedMessages = await scenario.Service.GetStaffMessagesAsync(
            LiveSupportTestData.AdminId, true, scenario.Conversation.Id, 50, CancellationToken.None);

        var loaded = Assert.Single(loadedMessages);
        Assert.Equal("Pending", loaded.ExternalDeliveryStatus);
        Assert.Equal("مدير الدعم", loaded.SenderDisplayName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaffWhatsAppMessage_WhenEditedOrDeleted_IsRejected(bool edit)
    {
        await using var scenario = await CreateStaffWhatsAppScenarioAsync();
        var message = Assert.Single(scenario.Db.LiveSupportMessages);

        var error = edit
            ? await Assert.ThrowsAsync<LiveSupportException>(() => scenario.Service.UpdateStaffMessageAsync(
                LiveSupportTestData.AdminId, true, scenario.Conversation.Id, message.Id, "تعديل", CancellationToken.None))
            : await Assert.ThrowsAsync<LiveSupportException>(() => scenario.Service.DeleteStaffMessageAsync(
                LiveSupportTestData.AdminId, true, scenario.Conversation.Id, message.Id, CancellationToken.None));

        Assert.Equal(LiveSupportErrorCodes.WhatsAppMessageImmutable, error.Code);
    }

    [Fact]
    public async Task AdminDashboard_MapsWhatsAppDetailsAndOperationalSummary()
    {
        await using var db = TestAppDbContextFactory.Create();
        var guest = LiveSupportTestData.Guest();
        var lastInboundAt = DateTime.UtcNow.AddMinutes(-5);
        var windowExpiresAt = DateTime.UtcNow.AddHours(20);
        var lastOutboundAt = DateTime.UtcNow.AddMinutes(-2);
        var lastTemplateSyncAt = DateTime.UtcNow.AddMinutes(-1);
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        var binding = Binding(conversation.Id, guest, windowExpiresAt);
        binding.LastInboundAt = lastInboundAt;
        db.LiveSupportWhatsAppBindings.Add(binding);
        db.LiveSupportWhatsAppMessages.Add(new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id,
            Direction = "Outbound",
            MessageType = "text",
            Status = "Failed",
            CreatedAt = lastOutboundAt,
            Version = 1
        });
        db.LiveSupportWhatsAppTemplates.Add(new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = "template-1",
            Name = "approved_template",
            Language = "ar",
            Category = "UTILITY",
            Status = "APPROVED",
            LastSyncedAt = lastTemplateSyncAt,
            Version = 1
        });
        await db.SaveChangesAsync();
        var service = new LiveSupportService(db, new LiveSupportEnabledSettings());

        var dashboard = await service.GetAdminDashboardAsync(CancellationToken.None);

        var row = Assert.Single(dashboard.Conversations);
        Assert.Equal("WhatsApp", row.Channel);
        Assert.Equal(guest.PhoneNumber, row.ExternalPhoneNumber);
        Assert.Equal(windowExpiresAt, row.CustomerServiceWindowExpiresAt);
        Assert.Equal("Failed", row.LastExternalDeliveryStatus);
        Assert.Equal(1, dashboard.WhatsApp.Open);
        Assert.Equal(1, dashboard.WhatsApp.Waiting);
        Assert.Equal(0, dashboard.WhatsApp.Active);
        Assert.Equal(0, dashboard.WhatsApp.ClosedToday);
        Assert.Equal(1, dashboard.WhatsApp.FailedOutbound);
        Assert.Equal(1, dashboard.WhatsApp.ApprovedTemplates);
        Assert.Equal(lastInboundAt, dashboard.WhatsApp.LastInboundAt);
        Assert.Equal(lastOutboundAt, dashboard.WhatsApp.LastOutboundAt);
        Assert.Equal(lastTemplateSyncAt, dashboard.WhatsApp.LastTemplateSyncAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AiReply_OutsideWhatsAppWindow_HandsOffWithoutRecordingUndeliveredReply(bool windowOpen)
    {
        await using var db = TestAppDbContextFactory.Create();
        var guest = LiveSupportTestData.Guest();
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        var source = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Guest,
            SenderGuestSessionId = guest.Id,
            ClientMessageId = Guid.NewGuid().ToString("N"),
            Type = LiveSupportMessageType.Text,
            Content = "رسالة واردة",
            SentAt = DateTime.UtcNow
        };
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "اختبار",
            CreatedByUserId = Guid.NewGuid(),
            Version = 1
        };
        var turn = new LiveSupportAITurn
        {
            ConversationId = conversation.Id,
            SourceMessageId = source.Id,
            PolicyVersionId = policy.Id,
            ExpectedConversationVersion = conversation.Version,
            Status = LiveSupportAITurnStatus.Processing,
            CallbackStatus = LiveSupportAICallbackStatus.NotReady,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        db.LiveSupportMessages.Add(source);
        db.LiveSupportAIPolicyVersions.Add(policy);
        db.LiveSupportAIConversationStates.Add(new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            Mode = LiveSupportAIMode.AiActive,
            PolicyVersionId = policy.Id,
            LastParticipantActivityAt = DateTime.UtcNow,
            Version = 1
        });
        db.LiveSupportAITurns.Add(turn);
        db.LiveSupportWhatsAppBindings.Add(Binding(
            conversation.Id,
            guest,
            windowOpen ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();
        var decision = new LiveSupportAIWorkerDecisionDto("1", "reply", "رد المساعد", null, null, null, null, null);
        var request = new LiveSupportAIWorkerCompletionDto(
            "1",
            conversation.Version,
            policy.Id,
            decision,
            LiveSupportAITurnOrchestrator.ComputeDecisionHash(decision),
            $"callback-{Guid.NewGuid():N}",
            "test-provider",
            "test-model",
            null,
            10,
            5,
            20);
        var orchestrator = new LiveSupportAITurnOrchestrator(db, null!);

        var outcome = await orchestrator.CompleteAsync(turn.Id, request, CancellationToken.None);

        if (windowOpen)
        {
            Assert.Equal("COMPLETED", outcome);
            var aiMessage = Assert.Single(db.LiveSupportMessages.Where(message => message.SenderType == LiveSupportSenderType.AI));
            var outbound = Assert.Single(db.LiveSupportWhatsAppMessages);
            Assert.Equal(aiMessage.Id, outbound.LiveSupportMessageId);
            Assert.Equal("Pending", outbound.Status);
        }
        else
        {
            Assert.Equal("FAILED_AND_HANDED_OFF", outcome);
            Assert.Empty(db.LiveSupportMessages.Where(message => message.SenderType == LiveSupportSenderType.AI));
            Assert.Empty(db.LiveSupportWhatsAppMessages);
            Assert.Equal("WHATSAPP_WINDOW_EXPIRED", turn.FailureCode);
            Assert.Equal(LiveSupportAITurnStatus.Failed, turn.Status);
            Assert.Equal(LiveSupportAIMode.HumanQueued,
                db.LiveSupportAIConversationStates.Single().Mode);
            Assert.Single(db.LiveSupportQueueEntries);
            Assert.Equal("REPLAYED",
                await orchestrator.CompleteAsync(turn.Id, request, CancellationToken.None));
        }
    }

    [Fact]
    public async Task StaffTemplateSend_UsesServerPreviewAndRejectsIdReuseForDifferentTemplate()
    {
        await using var scenario = await CreateStaffWhatsAppScenarioAsync();
        var firstTemplate = new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = "meta-template-1",
            Name = "welcome_one",
            Language = "ar",
            Category = "UTILITY",
            Status = "APPROVED",
            ComponentsJson = """
                [
                  {"type":"BODY","text":"كود {{2}} - أهلًا {{1}}"},
                  {"type":"BUTTONS","buttons":[
                    {"type":"URL","text":"فتح التقرير","url":"https://massar-academy.net/report"},
                    {"type":"URL","text":"فتح المنصة","url":"https://massar-academy.net"}
                  ]}
                ]
                """,
            Fingerprint = new string('a', 64),
            LastSyncedAt = DateTime.UtcNow,
            Version = 1
        };
        var secondTemplate = new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = "meta-template-2",
            Name = "welcome_two",
            Language = "ar",
            Category = "UTILITY",
            Status = "APPROVED",
            ComponentsJson = firstTemplate.ComponentsJson,
            Fingerprint = new string('b', 64),
            LastSyncedAt = DateTime.UtcNow,
            Version = 1
        };
        scenario.Db.LiveSupportWhatsAppTemplates.AddRange(firstTemplate, secondTemplate);
        await scenario.Db.SaveChangesAsync();
        var clientMessageId = Guid.NewGuid().ToString("N");

        var sent = await scenario.Service.SendStaffWhatsAppTemplateAsync(
            new SendLiveSupportWhatsAppTemplateCommand(
                LiveSupportTestData.AdminId,
                true,
                scenario.Conversation.Id,
                new SendLiveSupportWhatsAppTemplateRequest(
                    clientMessageId,
                    firstTemplate.Id,
                    [" أحمد ", " 123 "],
                    "نص لا يطابق القالب")),
            CancellationToken.None);

        Assert.Equal("كود 123 - أهلًا أحمد", sent.Message.Content);
        var delivery = scenario.Db.LiveSupportWhatsAppMessages.Single(item => item.LiveSupportMessageId == sent.Message.Id);
        Assert.Equal("welcome_one", delivery.TemplateName);
        var storedSnapshot = WhatsAppDirectTemplatePolicy.DeserializeParameterSnapshot(
            delivery.TemplateParametersJson);
        Assert.Equal(firstTemplate.Fingerprint, storedSnapshot?.Fingerprint);
        Assert.Equal(["أحمد", "123"], storedSnapshot?.Parameters);
        delivery.TemplateParametersJson = $$"""
            { "parameters": ["أحمد", "123"], "fingerprint": "{{firstTemplate.Fingerprint}}" }
            """;
        await scenario.Db.SaveChangesAsync();

        var replay = await scenario.Service.SendStaffWhatsAppTemplateAsync(
            new SendLiveSupportWhatsAppTemplateCommand(
                LiveSupportTestData.AdminId,
                true,
                scenario.Conversation.Id,
                new SendLiveSupportWhatsAppTemplateRequest(
                    clientMessageId,
                    firstTemplate.Id,
                    ["أحمد", "123"],
                    sent.Message.Content)),
            CancellationToken.None);
        Assert.True(replay.Replayed);

        var conflict = await Assert.ThrowsAsync<LiveSupportException>(() =>
            scenario.Service.SendStaffWhatsAppTemplateAsync(
                new SendLiveSupportWhatsAppTemplateCommand(
                    LiveSupportTestData.AdminId,
                    true,
                    scenario.Conversation.Id,
                    new SendLiveSupportWhatsAppTemplateRequest(
                        clientMessageId,
                        secondTemplate.Id,
                        ["أحمد", "123"],
                        sent.Message.Content)),
                CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.MessageConflict, conflict.Code);
    }

    public static TheoryData<string, string[], string> RejectedDirectTemplateCases => new()
    {
        {
            """[{"type":"HEADER","format":"IMAGE"},{"type":"BODY","text":"خبر جديد"}]""",
            [],
            ValidTemplateFingerprint
        },
        {
            """[{"type":"BODY","text":"مرحبًا {{1}}"},{"type":"BUTTONS","buttons":[{"type":"URL","text":"فتح","url":"https://massar-academy.net/{{1}}"}]}]""",
            ["student-token"],
            ValidTemplateFingerprint
        },
        {
            """[{"type":"BODY","text":"خبر جديد"},{"type":"BUTTONS","buttons":[{"type":"QUICK_REPLY","text":"رد"}]}]""",
            [],
            ValidTemplateFingerprint
        },
        {
            """[{"type":"HEADER","format":"TEXT","text":"عنوان فقط"}]""",
            [],
            ValidTemplateFingerprint
        },
        {
            """[{"type":"BODY","text":"مرحبًا {{1}} ثم {{1}}"}]""",
            ["أحمد"],
            ValidTemplateFingerprint
        },
        {
            """[{"type":"BODY","text":"مرحبًا {{ 1 }}"}]""",
            ["أحمد"],
            ValidTemplateFingerprint
        },
        {
            """[{"type":"BODY","text":"خبر جديد"}]""",
            [],
            string.Empty
        },
        {
            """[{"type":"BODY","text":"خبر جديد"}]""",
            [],
            new string('z', 64)
        }
    };

    [Theory]
    [MemberData(nameof(RejectedDirectTemplateCases))]
    public async Task StaffTemplateSend_RejectsUnsafeTemplateBeforePersistingMessage(
        string componentsJson,
        string[] parameters,
        string fingerprint)
    {
        await using var scenario = await CreateStaffWhatsAppScenarioAsync();
        var template = new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = $"meta-{Guid.NewGuid():N}",
            Name = $"unsafe_{Guid.NewGuid():N}",
            Language = "ar",
            Category = "UTILITY",
            Status = "APPROVED",
            ComponentsJson = componentsJson,
            Fingerprint = fingerprint,
            LastSyncedAt = DateTime.UtcNow,
            Version = 1
        };
        scenario.Db.LiveSupportWhatsAppTemplates.Add(template);
        await scenario.Db.SaveChangesAsync();
        var messageCount = await scenario.Db.LiveSupportMessages.CountAsync();
        var deliveryCount = await scenario.Db.LiveSupportWhatsAppMessages.CountAsync();

        var failure = await Assert.ThrowsAsync<LiveSupportException>(() =>
            scenario.Service.SendStaffWhatsAppTemplateAsync(
                new SendLiveSupportWhatsAppTemplateCommand(
                    LiveSupportTestData.AdminId,
                    true,
                    scenario.Conversation.Id,
                    new SendLiveSupportWhatsAppTemplateRequest(
                        Guid.NewGuid().ToString("N"),
                        template.Id,
                        parameters,
                        "معاينة غير موثوقة")),
                CancellationToken.None));

        Assert.Equal("WHATSAPP_TEMPLATE_PARAMETERS_INVALID", failure.Code);
        Assert.Equal(messageCount, await scenario.Db.LiveSupportMessages.CountAsync());
        Assert.Equal(deliveryCount, await scenario.Db.LiveSupportWhatsAppMessages.CountAsync());
    }

    private static async Task<StaffWhatsAppScenario> CreateStaffWhatsAppScenarioAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new CountingAppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var guest = LiveSupportTestData.Guest();
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Assigned,
            CurrentOwnerUserId = LiveSupportTestData.AdminId,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        db.Users.Add(LiveSupportTestData.User(LiveSupportTestData.AdminId, "مدير الدعم", "01011111111"));
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        db.LiveSupportWhatsAppBindings.Add(Binding(conversation.Id, guest, DateTime.UtcNow.AddHours(24)));
        await db.SaveChangesAsync();
        db.ResetSaveCount();
        var service = new LiveSupportService(db, new LiveSupportEnabledSettings());
        var sendResult = await service.SendStaffMessageAsync(
            LiveSupportTestData.AdminId,
            true,
            conversation.Id,
            Guid.NewGuid().ToString("N"),
            "رد واتساب",
            null,
            CancellationToken.None);
        return new StaffWhatsAppScenario(connection, db, service, conversation, sendResult);
    }

    private static WhatsAppCloudService Cloud(HttpMessageHandler handler, bool includeBusinessAccount = false)
    {
        var values = new Dictionary<string, string?>
        {
            ["WhatsAppCloudApi:AccessToken"] = "test-token",
            ["WhatsAppCloudApi:PhoneNumberId"] = "phone-id"
        };
        if (includeBusinessAccount) values["WhatsAppCloudApi:BusinessAccountId"] = "business-id";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new WhatsAppCloudService(
            new HttpClient(handler), configuration, NullLogger<WhatsAppCloudService>.Instance);
    }

    private static WhatsAppLiveSupportService Service(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        WhatsAppCloudService cloud) =>
        new(db, new LiveSupportService(db, new LiveSupportEnabledSettings()),
            new RejectingAttachmentStorage(), cloud, new LiveSupportEventWriter(db), ChannelConfiguration(),
            new StubWhatsAppCampaignService());

    private static IConfiguration ChannelConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsAppCloudApi:BusinessAccountId"] = "business-id",
            ["WhatsAppCloudApi:PhoneNumberId"] = "phone-id"
        }).Build();

    private static async Task<(LiveSupportGuestSession Guest, LiveSupportConversation Conversation)> SeedConversationAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var guest = LiveSupportTestData.Guest();
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        db.LiveSupportWhatsAppBindings.Add(new LiveSupportWhatsAppBinding
        {
            ConversationId = conversation.Id,
            GuestSessionId = guest.Id,
            WhatsAppUserId = "201099999999",
            PhoneNumber = "01099999999",
            DisplayName = guest.DisplayName,
            LastInboundAt = DateTime.UtcNow,
            CustomerServiceWindowExpiresAt = DateTime.UtcNow.AddHours(24),
            Version = 1
        });
        await db.SaveChangesAsync();
        return (guest, conversation);
    }

    private static LiveSupportWhatsAppBinding Binding(
        Guid conversationId,
        LiveSupportGuestSession guest,
        DateTime windowExpiresAt) => new()
    {
        ConversationId = conversationId,
        GuestSessionId = guest.Id,
        WhatsAppUserId = "201099999999",
        PhoneNumber = guest.PhoneNumber,
        DisplayName = guest.DisplayName,
        LastInboundAt = DateTime.UtcNow,
        CustomerServiceWindowExpiresAt = windowExpiresAt,
        Version = 1
    };

    private static LiveSupportWhatsAppPendingReceipt PendingReceipt(string metaMessageId, DateTime createdAt) => new()
    {
        MetaMessageId = metaMessageId,
        Status = "Sent",
        ProviderTimestamp = createdAt,
        CreatedAt = createdAt,
        Version = 1
    };

    private static async Task ProcessStatusAsync(
        WhatsAppLiveSupportService service,
        string status,
        long timestamp,
        int? errorCode = null)
    {
        var errors = errorCode.HasValue ? $",\"errors\":[{{\"code\":{errorCode.Value}}}]" : string.Empty;
        using var webhook = JsonDocument.Parse(
            $"{{\"object\":\"whatsapp_business_account\",\"entry\":[{{\"id\":\"business-id\",\"changes\":[{{\"value\":{{\"metadata\":{{\"phone_number_id\":\"phone-id\"}},\"statuses\":[{{\"id\":\"wamid.receipt-1\",\"status\":\"{status}\",\"timestamp\":\"{timestamp}\"{errors}}}]}}}}]}}]}}");
        await service.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);
    }

    private static string ChannelWebhookJson(
        string messageId,
        long timestamp,
        string businessAccountId = "business-id",
        string phoneNumberId = "phone-id") =>
        WebhookJson
            .Replace("wamid.test-1", messageId, StringComparison.Ordinal)
            .Replace("1787529600", timestamp.ToString(), StringComparison.Ordinal)
            .Replace("business-id", businessAccountId, StringComparison.Ordinal)
            .Replace("phone-id", phoneNumberId, StringComparison.Ordinal);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private const string WebhookJson = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "business-id",
            "changes": [{
              "value": {
                "metadata": { "phone_number_id": "phone-id" },
                "contacts": [{ "wa_id": "201099999999", "profile": { "name": "عميل واتساب" } }],
                "messages": [{
                  "id": "wamid.test-1",
                  "from": "201099999999",
                  "timestamp": "1787529600",
                  "type": "text",
                  "text": { "body": "رسالة اختبار" }
                }]
              }
            }]
          }]
        }
        """;

    private const string MediaWebhookJson = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "business-id",
            "changes": [{
              "value": {
                "metadata": { "phone_number_id": "phone-id" },
                "contacts": [{ "wa_id": "201099999999", "profile": { "name": "عميل واتساب" } }],
                "messages": [{
                  "id": "wamid.media-1",
                  "from": "201099999999",
                  "timestamp": "1787529600",
                  "type": "image",
                  "image": { "id": "media-1", "caption": "صورة" }
                }]
              }
            }]
          }]
        }
        """;

    private const string DocumentWebhookJson = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "business-id",
            "changes": [{
              "value": {
                "metadata": { "phone_number_id": "phone-id" },
                "contacts": [{ "wa_id": "201099999999", "profile": { "name": "عميل واتساب" } }],
                "messages": [{
                  "id": "wamid.document-1",
                  "from": "201099999999",
                  "timestamp": "1787529600",
                  "type": "document",
                  "document": { "id": "media-document-1", "caption": "مستند" }
                }]
              }
            }]
          }]
        }
        """;

    private sealed class RejectingAttachmentStorage : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct) => throw new InvalidOperationException("No media expected in this scenario.");
        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) => throw new InvalidOperationException("No media expected in this scenario.");
        public Task DeleteAsync(string storagePath, CancellationToken ct) => throw new InvalidOperationException("No media expected in this scenario.");
    }

    private sealed class RecordingMetaHandler : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken)));
            var body = Requests.Count == 1 ? "{\"id\":\"media-1\"}" : "{\"messages\":[{\"id\":\"wamid.sent\"}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StubMetaHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return Task.FromResult(response(request));
        }
    }

    private sealed class BodyRecordingMetaHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return JsonResponse(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.sent\"}]}");
        }
    }

    private sealed class CountingReadStream(long availableBytes) : Stream
    {
        private long _remaining = availableBytes;

        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadCore(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ReadCore(buffer.Span));

        private int ReadCore(Span<byte> buffer)
        {
            var count = (int)Math.Min(_remaining, buffer.Length);
            if (count == 0) return 0;
            buffer[..count].Clear();
            _remaining -= count;
            BytesRead += count;
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CountingAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public int SaveCount { get; private set; }

        public void ResetSaveCount() => SaveCount = 0;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record StaffWhatsAppScenario(
        SqliteConnection Connection,
        CountingAppDbContext Db,
        LiveSupportService Service,
        LiveSupportConversation Conversation,
        LiveSupportSendResultDto SendResult) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
