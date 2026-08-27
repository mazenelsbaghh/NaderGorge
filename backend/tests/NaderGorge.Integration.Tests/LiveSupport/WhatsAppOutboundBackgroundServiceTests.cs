using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class WhatsAppOutboundBackgroundServiceTests
{
    [Fact]
    public async Task ProductionRegression_20260826_StaffTemplateWithStaticUrls_SendsOnlyBodyParameters()
    {
        var handler = new RecordingMessageMetaHandler();
        await using var harness = await Harness.CreateAsync(handler);
        Guid deliveryId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            deliveryId = AddTemplateDelivery(db, """
                [
                  {"type":"HEADER","format":"TEXT","text":"Student progress"},
                  {"type":"BODY","text":"Hello {{1}}, score {{2}}"},
                  {"type":"BUTTONS","buttons":[
                    {"type":"URL","text":"Open report","url":"https://massar-academy.net/report"},
                    {"type":"URL","text":"Open platform","url":"https://massar-academy.net"}
                  ]}
                ]
                """, ["Mazen", "95%"]).Id;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        using var request = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var components = request.RootElement.GetProperty("template").GetProperty("components");
        var body = Assert.Single(components.EnumerateArray());
        Assert.Equal("body", body.GetProperty("type").GetString());
        Assert.Equal("Mazen", body.GetProperty("parameters")[0].GetProperty("text").GetString());
        Assert.Equal("95%", body.GetProperty("parameters")[1].GetProperty("text").GetString());
        await using var assertionScope = harness.Services.CreateAsyncScope();
        var delivery = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal("Sent", delivery.Status);
    }

    [Fact]
    public async Task LegacyStaffTemplateWithStaticPhoneButton_OmitsButtonRuntimeComponent()
    {
        var handler = new RecordingMessageMetaHandler();
        await using var harness = await Harness.CreateAsync(handler);
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AddTemplateDelivery(db, """
                [
                  {"type":"BODY","text":"Hello {{1}}"},
                  {"type":"BUTTONS","buttons":[
                    {"type":"PHONE_NUMBER","text":"Call","phone_number":"+201000000000"}
                  ]}
                ]
                """, ["Mazen"], legacyPayload: true);
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        using var request = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var components = request.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal("body", Assert.Single(components.EnumerateArray()).GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("{\"type\":\"HEADER\",\"format\":\"IMAGE\"}")]
    [InlineData("{\"type\":\"BUTTONS\",\"buttons\":[{\"type\":\"URL\",\"text\":\"Open\",\"url\":\"https://massar-academy.net/{{1}}\"}]}")]
    [InlineData("{\"type\":\"BUTTONS\",\"buttons\":[{\"type\":\"QUICK_REPLY\",\"text\":\"Reply\"}]}")]
    [InlineData("{\"type\":\"BUTTONS\",\"buttons\":[{\"type\":\"FLOW\",\"text\":\"Open\"}]}")]
    [InlineData("{\"type\":\"CAROUSEL\"}")]
    public async Task StaffTemplateWithUnsupportedRuntimeShape_FailsBeforeProvider(string component)
    {
        var handler = new StubMetaHandler(_ => throw new InvalidOperationException("No provider call expected."));
        await using var harness = await Harness.CreateAsync(handler);
        Guid deliveryId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            deliveryId = AddTemplateDelivery(db,
                $"[{{\"type\":\"BODY\",\"text\":\"Hello\"}},{component}]", []).Id;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
        await using var assertionScope = harness.Services.CreateAsyncScope();
        var delivery = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("WHATSAPP_TEMPLATE_PARAMETERS_INVALID", delivery.FailureCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-fingerprint")]
    public async Task StaffTemplateWithoutSynchronizedFingerprint_FailsBeforeProvider(string fingerprint)
    {
        var handler = new StubMetaHandler(_ => throw new InvalidOperationException("No provider call expected."));
        await using var harness = await Harness.CreateAsync(handler);
        Guid deliveryId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            deliveryId = AddTemplateDelivery(
                db,
                """[{"type":"BODY","text":"Hello"}]""",
                [],
                fingerprint).Id;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
        await using var assertionScope = harness.Services.CreateAsyncScope();
        var delivery = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("WHATSAPP_TEMPLATE_PARAMETERS_INVALID", delivery.FailureCode);
    }

    [Fact]
    public async Task StaffTemplateChangedAfterQueue_FailsWithDriftBeforeProvider()
    {
        var handler = new StubMetaHandler(_ => throw new InvalidOperationException("No provider call expected."));
        await using var harness = await Harness.CreateAsync(handler);
        Guid deliveryId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            deliveryId = AddTemplateDelivery(
                db,
                """[{"type":"BODY","text":"Hello {{1}}"}]""",
                ["Mazen"]).Id;
            await db.SaveChangesAsync();
            var template = await db.LiveSupportWhatsAppTemplates.SingleAsync();
            template.ComponentsJson = """[{"type":"BODY","text":"Changed {{1}}"}]""";
            template.Fingerprint = new string('b', 64);
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
        await using var assertionScope = harness.Services.CreateAsyncScope();
        var delivery = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("WHATSAPP_TEMPLATE_DRIFT", delivery.FailureCode);
    }

    [Fact]
    public async Task ProductionRegression_20260825_StoredWebp_IsNormalizedBeforeProviderUpload()
    {
        await using var webp = new MemoryStream();
        using (var image = new Image<Rgba32>(2, 2, new Rgba32(20, 40, 60, 255)))
            await image.SaveAsWebpAsync(webp, CancellationToken.None);
        var storedBytes = webp.ToArray();
        var handler = new RecordingMediaMetaHandler();
        var storage = new MemoryAttachmentStorage(storedBytes);
        var normalizer = new WhatsAppOutboundMediaNormalizer(new RejectingAudioProcess());
        await using var harness = await Harness.CreateAsync(handler, storage, normalizer);
        Guid attachmentId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            attachmentId = AddMediaDelivery(db, LiveSupportMessageType.Image,
                "legacy.webp", "image/webp", storedBytes.Length).AttachmentId!.Value;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("Content-Type: image/jpeg", handler.Requests[0].Body);
        Assert.Contains("legacy.jpg", handler.Requests[0].Body);
        Assert.DoesNotContain("image/webp", handler.Requests[0].Body);
        await using var assertionScope = harness.Services.CreateAsyncScope();
        var attachment = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportAttachments.SingleAsync(item => item.Id == attachmentId);
        Assert.Equal("image/webp", attachment.ContentType);
        Assert.Equal("legacy.webp", attachment.OriginalFileName);
    }

    [Fact]
    public async Task ProductionRegression_20260825_StoredWebm_IsNormalizedBeforeProviderUpload()
    {
        byte[] storedBytes = [0x1A, 0x45, 0xDF, 0xA3, 1, 2, 3];
        byte[] normalizedBytes = [0x4F, 0x67, 0x67, 0x53, 4, 5, 6];
        var handler = new RecordingMediaMetaHandler();
        var storage = new MemoryAttachmentStorage(storedBytes);
        var normalizer = new WhatsAppOutboundMediaNormalizer(
            new FixedAudioProcess(normalizedBytes));
        await using var harness = await Harness.CreateAsync(handler, storage, normalizer);
        Guid attachmentId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            attachmentId = AddMediaDelivery(db, LiveSupportMessageType.Audio,
                "legacy.webm", "audio/webm", storedBytes.Length).AttachmentId!.Value;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("Content-Type: audio/ogg", handler.Requests[0].Body);
        Assert.Contains("legacy.ogg", handler.Requests[0].Body);
        Assert.DoesNotContain("audio/webm", handler.Requests[0].Body);
        Assert.DoesNotContain("\"voice\"", handler.Requests[1].Body);
        Assert.DoesNotContain("caption", handler.Requests[1].Body);
        await using var assertionScope = harness.Services.CreateAsyncScope();
        var attachment = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportAttachments.SingleAsync(item => item.Id == attachmentId);
        Assert.Equal("audio/webm", attachment.ContentType);
        Assert.Equal("legacy.webm", attachment.OriginalFileName);
    }

    [Fact]
    public async Task UnexpectedFailure_IsRecoveredWithoutBlockingTheNextMessage()
    {
        var handler = new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.sent\"}]}"));
        await using var harness = await Harness.CreateAsync(handler);
        Guid firstId;
        Guid secondId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            firstId = AddDelivery(db, withBinding: false, createdAt: DateTime.UtcNow.AddMinutes(-2)).Id;
            secondId = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-1)).Id;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        await using var assertionScope = harness.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await assertionDb.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == firstId);
        var second = await assertionDb.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == secondId);
        Assert.Equal("Pending", first.Status);
        Assert.Equal(1, first.AttemptCount);
        Assert.Equal("WHATSAPP_DISPATCH_FAILED", first.FailureCode);
        Assert.NotNull(first.NextAttemptAt);
        Assert.Null(first.ClaimedAt);
        Assert.Equal("Sent", second.Status);
        Assert.Equal(1, second.AttemptCount);
        Assert.Equal("wamid.sent", second.MetaMessageId);
        Assert.Null(second.ClaimedAt);
        Assert.Single(handler.Requests);
        Assert.Equal(2, await assertionDb.LiveSupportEvents.CountAsync(item =>
            item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
    }

    [Fact]
    public async Task FifthAttemptFailure_IsTerminalAndNotRetriedAgain()
    {
        var handler = new StubMetaHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        await using var harness = await Harness.CreateAsync(handler);
        Guid fifthAttemptId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seededFifthAttempt = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-2));
            seededFifthAttempt.AttemptCount = 4;
            fifthAttemptId = seededFifthAttempt.Id;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);
        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        await using var assertionScope = harness.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fifthAttempt = await assertionDb.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == fifthAttemptId);
        Assert.Equal("Failed", fifthAttempt.Status);
        Assert.Equal(5, fifthAttempt.AttemptCount);
        Assert.Equal("WHATSAPP_CLOUD_INVALID_RESPONSE", fifthAttempt.FailureCode);
        Assert.Null(fifthAttempt.NextAttemptAt);
        Assert.Null(fifthAttempt.ClaimedAt);
        Assert.Single(handler.Requests);
        Assert.Equal(1, await assertionDb.LiveSupportEvents.CountAsync(item =>
            item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged));
        Assert.All(await assertionDb.OutboxEvents.Where(item => item.Type == "LiveSupportEvent").ToListAsync(), item =>
        {
            Assert.DoesNotContain("wamid.sent", item.PayloadJson);
            Assert.DoesNotContain("01022222222", item.PayloadJson);
        });
    }

    [Fact]
    public async Task StaleSending_IsTerminalUncertainAndNeverRetried()
    {
        var handler = new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.duplicate\"}]}"));
        await using var harness = await Harness.CreateAsync(handler);
        Guid deliveryId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seededDelivery = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-10));
            seededDelivery.Status = "Sending";
            seededDelivery.ClaimedAt = DateTime.UtcNow.AddMinutes(-10);
            seededDelivery.AttemptCount = 1;
            deliveryId = seededDelivery.Id;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);
        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        await using var assertionScope = harness.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var delivery = await assertionDb.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("WHATSAPP_DELIVERY_UNCERTAIN", delivery.FailureCode);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Null(delivery.ClaimedAt);
        Assert.Null(delivery.NextAttemptAt);
        Assert.Empty(handler.Requests);
        Assert.Single(await assertionDb.LiveSupportEvents.Where(item =>
            item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged).ToListAsync());
    }

    [Fact]
    public async Task ProviderAcceptanceFollowedByPersistenceFailure_IsTerminalAndNeverSentTwice()
    {
        var handler = new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.accepted\"}]}"));
        await using var harness = await Harness.CreateAsync(handler);
        Guid deliveryId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            deliveryId = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-1)).Id;
            await db.SaveChangesAsync();
        }
        harness.ArmPersistenceFault();

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);
        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        await using var assertionScope = harness.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var delivery = await assertionDb.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("WHATSAPP_DELIVERY_UNCERTAIN", delivery.FailureCode);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Null(delivery.MetaMessageId);
        Assert.Single(handler.Requests);
        Assert.Single(await assertionDb.LiveSupportEvents.Where(item =>
            item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged).ToListAsync());
    }

    [Fact]
    public async Task NewerMessage_DoesNotOvertakeAnOlderPendingMessageInTheSameConversation()
    {
        var handler = new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.ordered\"}]}"));
        await using var harness = await Harness.CreateAsync(handler);
        Guid olderId;
        Guid newerId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var older = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-2));
            older.NextAttemptAt = DateTime.UtcNow.AddHours(1);
            var newer = AddDeliveryInConversation(db, older.ConversationId, DateTime.UtcNow.AddMinutes(-1));
            olderId = older.Id;
            newerId = newer.Id;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);
        Assert.Empty(handler.Requests);

        await using (var releaseScope = harness.Services.CreateAsyncScope())
        {
            var db = releaseScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var older = await db.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == olderId);
            var newer = await db.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == newerId);
            Assert.Equal("Pending", older.Status);
            Assert.Equal("Pending", newer.Status);
            Assert.Equal(0, newer.AttemptCount);
            older.Status = "Failed";
            older.NextAttemptAt = null;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        await using var assertionScope = harness.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sent = await assertionDb.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == newerId);
        Assert.Equal("Sent", sent.Status);
        Assert.Equal("wamid.ordered", sent.MetaMessageId);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ReceiptArrivingBeforeProviderIdPersistence_IsReconciledWithoutDuplicateSentEvent()
    {
        const string metaMessageId = "wamid.receipt-before-save";
        var handler = new StubMetaHandler(_ => JsonResponse(
            HttpStatusCode.OK, $"{{\"messages\":[{{\"id\":\"{metaMessageId}\"}}]}}"));
        await using var harness = await Harness.CreateAsync(handler);
        var receiptAt = DateTime.UtcNow.AddMinutes(-1);
        Guid deliveryId;
        Guid supportMessageId;
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seededDelivery = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-2));
            deliveryId = seededDelivery.Id;
            supportMessageId = seededDelivery.LiveSupportMessageId!.Value;
            db.LiveSupportWhatsAppPendingReceipts.Add(new LiveSupportWhatsAppPendingReceipt
            {
                MetaMessageId = metaMessageId,
                Status = "Read",
                ProviderTimestamp = receiptAt,
                DeliveredAt = receiptAt,
                ReadAt = receiptAt,
                Version = 1
            });
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        await using var assertionScope = harness.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var delivery = await assertionDb.LiveSupportWhatsAppMessages.SingleAsync(item => item.Id == deliveryId);
        var message = await assertionDb.LiveSupportMessages.SingleAsync(item => item.Id == supportMessageId);
        Assert.Equal("Read", delivery.Status);
        Assert.Equal(metaMessageId, delivery.MetaMessageId);
        Assert.Equal(receiptAt, delivery.DeliveredAt);
        Assert.Equal(receiptAt, delivery.ReadAt);
        Assert.Equal(receiptAt, message.DeliveredAt);
        Assert.Equal(receiptAt, message.ReadAt);
        Assert.Empty(await assertionDb.LiveSupportWhatsAppPendingReceipts.ToListAsync());
        var deliveryEvent = Assert.Single(await assertionDb.LiveSupportEvents.Where(item =>
            item.Type == LiveSupportEventType.WhatsAppDeliveryStatusChanged).ToListAsync());
        Assert.Contains("\"status\":\"Read\"", deliveryEvent.SafeMetadataJson);
        Assert.DoesNotContain("\"status\":\"Sent\"", deliveryEvent.SafeMetadataJson);
    }

    [Fact]
    public async Task PendingReceiptCleanup_RunsOnceAndIsThrottledForTwentyFourHours()
    {
        await using var harness = await Harness.CreateAsync(new StubMetaHandler(_ =>
            throw new InvalidOperationException("No provider call expected.")));
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.LiveSupportWhatsAppPendingReceipts.Add(PendingReceipt(
                "wamid.expired-before-cleanup", DateTime.UtcNow.AddDays(-31)));
            await db.SaveChangesAsync();
        }

        await harness.Worker.CleanupPendingReceiptsIfDueAsync(CancellationToken.None);

        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await db.LiveSupportWhatsAppPendingReceipts.ToListAsync());
            db.LiveSupportWhatsAppPendingReceipts.Add(PendingReceipt(
                "wamid.expired-after-cleanup", DateTime.UtcNow.AddDays(-31)));
            await db.SaveChangesAsync();
        }

        await harness.Worker.CleanupPendingReceiptsIfDueAsync(CancellationToken.None);

        await using var assertionScope = harness.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("wamid.expired-after-cleanup",
            Assert.Single(await assertionDb.LiveSupportWhatsAppPendingReceipts.ToListAsync()).MetaMessageId);
    }

    private static LiveSupportWhatsAppMessage AddDelivery(
        AppDbContext db,
        bool withBinding,
        DateTime createdAt)
    {
        var guest = new LiveSupportGuestSession
        {
            DisplayName = "WhatsApp guest",
            PhoneNumber = withBinding ? "01022222222" : "01011111111",
            SecurityStampHash = Guid.NewGuid().ToString("N"),
            CreatedIpHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            LastSeenAt = DateTime.UtcNow,
            UserAgentSummary = "test"
        };
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        var message = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Staff,
            ClientMessageId = Guid.NewGuid().ToString("N"),
            Type = LiveSupportMessageType.Text,
            Content = "اختبار",
            SentAt = DateTime.UtcNow
        };
        var delivery = new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id,
            LiveSupportMessageId = message.Id,
            Direction = "Outbound",
            MessageType = "text",
            Status = "Pending",
            Version = 1,
            CreatedAt = createdAt
        };
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        db.LiveSupportMessages.Add(message);
        db.LiveSupportWhatsAppMessages.Add(delivery);
        if (withBinding)
        {
            db.LiveSupportWhatsAppBindings.Add(new LiveSupportWhatsAppBinding
            {
                ConversationId = conversation.Id,
                GuestSessionId = guest.Id,
                WhatsAppUserId = $"20{guest.PhoneNumber[1..]}",
                PhoneNumber = guest.PhoneNumber,
                DisplayName = guest.DisplayName,
                LastInboundAt = DateTime.UtcNow,
                CustomerServiceWindowExpiresAt = DateTime.UtcNow.AddHours(24),
                Version = 1
            });
        }
        return delivery;
    }

    private static LiveSupportWhatsAppMessage AddDeliveryInConversation(
        AppDbContext db,
        Guid conversationId,
        DateTime createdAt)
    {
        var message = new LiveSupportMessage
        {
            ConversationId = conversationId,
            SenderType = LiveSupportSenderType.Staff,
            ClientMessageId = Guid.NewGuid().ToString("N"),
            Type = LiveSupportMessageType.Text,
            Content = "رسالة أحدث",
            SentAt = DateTime.UtcNow
        };
        var delivery = new LiveSupportWhatsAppMessage
        {
            ConversationId = conversationId,
            LiveSupportMessageId = message.Id,
            Direction = "Outbound",
            MessageType = "text",
            Status = "Pending",
            Version = 1,
            CreatedAt = createdAt
        };
        db.LiveSupportMessages.Add(message);
        db.LiveSupportWhatsAppMessages.Add(delivery);
        return delivery;
    }

    private static LiveSupportMessage AddMediaDelivery(
        AppDbContext db,
        LiveSupportMessageType messageType,
        string fileName,
        string contentType,
        long sizeBytes)
    {
        var delivery = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-1));
        var message = db.LiveSupportMessages.Local.Single(item => item.Id == delivery.LiveSupportMessageId);
        var attachment = new LiveSupportAttachment
        {
            StoragePath = "stored/legacy",
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = new string('A', 64),
            UploadedByIdentity = "staff"
        };
        message.Type = messageType;
        message.Content = messageType == LiveSupportMessageType.Image ? "صورة" : "تسجيل صوتي";
        message.AttachmentId = attachment.Id;
        delivery.MessageType = messageType.ToString().ToLowerInvariant();
        db.LiveSupportAttachments.Add(attachment);
        return message;
    }

    private static LiveSupportWhatsAppMessage AddTemplateDelivery(
        AppDbContext db,
        string componentsJson,
        IReadOnlyList<string> parameters,
        string? fingerprint = null,
        bool legacyPayload = false)
    {
        var delivery = AddDelivery(db, withBinding: true, createdAt: DateTime.UtcNow.AddMinutes(-1));
        var templateFingerprint = fingerprint ?? new string('a', 64);
        delivery.MessageType = "template";
        delivery.TemplateName = "staff_template";
        delivery.TemplateLanguage = "ar";
        delivery.TemplateParametersJson = legacyPayload
            ? JsonSerializer.Serialize(parameters)
            : WhatsAppDirectTemplatePolicy.SerializeParameterSnapshot(templateFingerprint, parameters);
        db.LiveSupportWhatsAppTemplates.Add(new LiveSupportWhatsAppTemplate
        {
            MetaTemplateId = "meta-staff-template",
            Name = delivery.TemplateName,
            Language = delivery.TemplateLanguage,
            Category = "UTILITY",
            Status = "APPROVED",
            ComponentsJson = componentsJson,
            Fingerprint = templateFingerprint,
            LastSyncedAt = DateTime.UtcNow,
            Version = 1
        });
        return delivery;
    }

    private static LiveSupportWhatsAppPendingReceipt PendingReceipt(string metaMessageId, DateTime createdAt) => new()
    {
        MetaMessageId = metaMessageId,
        Status = "Sent",
        ProviderTimestamp = createdAt,
        CreatedAt = createdAt,
        Version = 1
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubMetaHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            return Task.FromResult(response(request));
        }
    }

    private sealed class RejectingAttachmentStorage : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct) =>
            throw new InvalidOperationException("No attachment write expected.");
        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No attachment read expected.");
        public Task DeleteAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No attachment delete expected.");
    }

    private sealed class RejectingMediaNormalizer : IWhatsAppOutboundMediaNormalizer
    {
        public Task<WhatsAppOutboundMedia> NormalizeAsync(
            WhatsAppOutboundMediaSource source,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No media normalization expected.");
    }

    private sealed class MemoryAttachmentStorage(byte[] content) : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(
            Stream source,
            string fileName,
            string contentType,
            long sizeBytes,
            CancellationToken ct) =>
            throw new InvalidOperationException("No attachment write expected.");

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(content, writable: false));

        public Task DeleteAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No attachment delete expected.");
    }

    private sealed class RejectingAudioProcess : IWhatsAppAudioProcess
    {
        public Task<byte[]> TranscodeToOggOpusMonoAsync(
            ReadOnlyMemory<byte> source,
            int maximumOutputBytes,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No audio conversion expected.");
    }

    private sealed class FixedAudioProcess(byte[] output) : IWhatsAppAudioProcess
    {
        public Task<byte[]> TranscodeToOggOpusMonoAsync(
            ReadOnlyMemory<byte> source,
            int maximumOutputBytes,
            CancellationToken cancellationToken) =>
            Task.FromResult(output);
    }

    private sealed class RecordingMediaMetaHandler : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));
            var responseBody = Requests.Count == 1
                ? "{\"id\":\"media-normalized\"}"
                : "{\"messages\":[{\"id\":\"wamid.normalized\"}]}";
            return JsonResponse(HttpStatusCode.OK, responseBody);
        }
    }

    private sealed class RecordingMessageMetaHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return JsonResponse(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.template\"}]}");
        }
    }

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate() { }
    }

    private sealed class ProviderPersistenceFault : SaveChangesInterceptor
    {
        private bool _armed;

        public void Arm() => _armed = true;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var shouldFail = _armed && eventData.Context?.ChangeTracker
                .Entries<LiveSupportWhatsAppMessage>()
                .Any(entry => entry.State == EntityState.Modified &&
                              entry.Entity.Status == "Sent" &&
                              !string.IsNullOrWhiteSpace(entry.Entity.MetaMessageId)) == true;
            if (!shouldFail) return base.SavingChangesAsync(eventData, result, cancellationToken);
            _armed = false;
            throw new DbUpdateException("Simulated provider-accepted persistence failure.");
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ProviderPersistenceFault _persistenceFault;

        private Harness(
            ServiceProvider services,
            SqliteConnection connection,
            ProviderPersistenceFault persistenceFault)
        {
            Services = services;
            _connection = connection;
            _persistenceFault = persistenceFault;
            Worker = new WhatsAppOutboundBackgroundService(
                services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<WhatsAppOutboundBackgroundService>.Instance);
        }

        public ServiceProvider Services { get; }
        public WhatsAppOutboundBackgroundService Worker { get; }

        public void ArmPersistenceFault() => _persistenceFault.Arm();

        public static async Task<Harness> CreateAsync(HttpMessageHandler handler)
        {
            return await CreateAsync(
                handler,
                new RejectingAttachmentStorage(),
                new RejectingMediaNormalizer());
        }

        public static async Task<Harness> CreateAsync(
            HttpMessageHandler handler,
            ILiveSupportAttachmentStorage attachmentStorage,
            IWhatsAppOutboundMediaNormalizer mediaNormalizer)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsAppCloudApi:AccessToken"] = "test-token",
                ["WhatsAppCloudApi:PhoneNumberId"] = "phone-id"
            }).Build();
            var cloud = new WhatsAppCloudService(
                new HttpClient(handler), configuration, NullLogger<WhatsAppCloudService>.Instance);
            var persistenceFault = new ProviderPersistenceFault();
            var services = new ServiceCollection()
                .AddDbContext<AppDbContext>(options => options
                    .UseSqlite(connection)
                    .AddInterceptors(persistenceFault))
                .AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>())
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<ICachedPlatformSettingsReader>(new EnabledSettings())
                .AddScoped<ILiveSupportService, LiveSupportService>()
                .AddScoped<ILiveSupportEventWriter, LiveSupportEventWriter>()
                .AddSingleton<IWhatsAppCampaignService>(new StubWhatsAppCampaignService())
                .AddScoped<WhatsAppLiveSupportService>()
                .AddSingleton<ILiveSupportAttachmentStorage>(attachmentStorage)
                .AddSingleton<IWhatsAppOutboundMediaNormalizer>(mediaNormalizer)
                .AddSingleton(cloud)
                .BuildServiceProvider();
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            return new Harness(services, connection, persistenceFault);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
