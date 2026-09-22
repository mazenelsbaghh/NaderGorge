using System.Text.Json;
using NaderGorge.Application.Services;
using NaderGorge.Application.Features.LiveSupport.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class BaileysSyncTests
{
    [Fact]
    public async Task Late_history_keeps_latest_activity_and_phone_reply_in_the_same_active_chat()
    {
        await using var db = TestAppDbContextFactory.Create();
        var account = new LiveSupportWhatsAppAccount { InstanceName = "account-a", Name = "Support" };
        var guest = LiveSupportTestData.Guest();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var inboundAt = DateTimeOffset.FromUnixTimeSeconds(now).UtcDateTime;
        var conversation = new LiveSupportConversation
        {
            GuestSessionId = guest.Id, ParticipantType = LiveSupportParticipantType.Guest,
            Status = LiveSupportConversationStatus.Waiting, LastMessageAt = null
        };
        db.LiveSupportWhatsAppAccounts.Add(account);
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        var binding = new LiveSupportWhatsAppBinding
        {
            AccountId = account.Id, ConversationId = conversation.Id, GuestSessionId = guest.Id,
            WhatsAppUserId = "201099999999", PhoneNumber = "201099999999", DisplayName = "Customer",
            LastInboundAt = inboundAt, CustomerServiceWindowExpiresAt = inboundAt.AddHours(24)
        };
        db.LiveSupportWhatsAppBindings.Add(binding);
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().Build();
        var live = new LiveSupportService(db, new LiveSupportEnabledSettings());
        var cloud = new WhatsAppCloudService(new HttpClient(), config, NullLogger<WhatsAppCloudService>.Instance);
        var support = new WhatsAppLiveSupportService(db, live, new NoMedia(), cloud,
            new LiveSupportEventWriter(db), config, new StubWhatsAppCampaignService());
        var webhook = new BaileysWebhookService(db, support);

        await webhook.ReceiveAsync(Payload(account.InstanceName, "mobile-now", true, false, now + 60), CancellationToken.None);
        Assert.Equal("Customer", binding.DisplayName);
        Assert.Equal(inboundAt.AddSeconds(60), conversation.LastMessageAt);
        await webhook.ReceiveAsync(Payload(account.InstanceName, "history-old", false, true, now - 86400), CancellationToken.None);

        Assert.Equal(inboundAt.AddSeconds(60), conversation.LastMessageAt);
        Assert.Equal(inboundAt, binding.LastInboundAt);
        Assert.Equal(inboundAt.AddHours(24), binding.CustomerServiceWindowExpiresAt);
        Assert.Equal(LiveSupportConversationStatus.Waiting, conversation.Status);
        Assert.Single(db.LiveSupportConversations);
        Assert.Equal(2, db.LiveSupportMessages.Count());
        Assert.DoesNotContain(db.LiveSupportWhatsAppMessages, item => item.Status == "Queued");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task History_and_phone_sent_messages_are_imported_once_without_new_work(bool fromMe)
    {
        await using var db = TestAppDbContextFactory.Create();
        var account = new LiveSupportWhatsAppAccount { InstanceName = "account-a", Name = "Support" };
        db.LiveSupportWhatsAppAccounts.Add(account);
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().Build();
        var live = new LiveSupportService(db, new LiveSupportEnabledSettings());
        var cloud = new WhatsAppCloudService(new HttpClient(), config, NullLogger<WhatsAppCloudService>.Instance);
        var support = new WhatsAppLiveSupportService(db, live, new NoMedia(), cloud,
            new LiveSupportEventWriter(db), config, new StubWhatsAppCampaignService());
        var webhook = new BaileysWebhookService(db, support);
        var time = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        var payload = Payload(account.InstanceName, "old-1", fromMe, true, time);
        await webhook.ReceiveAsync(payload, CancellationToken.None);
        await webhook.ReceiveAsync(payload, CancellationToken.None);
        var message = Assert.Single(db.LiveSupportMessages);
        Assert.Equal(fromMe ? LiveSupportSenderType.Staff : LiveSupportSenderType.Guest, message.SenderType);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime, message.SentAt);
        Assert.Equal(LiveSupportConversationStatus.Closed, Assert.Single(db.LiveSupportConversations).Status);
        Assert.Empty(db.LiveSupportQueueEntries);
        Assert.DoesNotContain(db.LiveSupportWhatsAppMessages, item => item.Status == "Queued");
        Assert.Single(db.LiveSupportWhatsAppMessages);

        // A reply typed on the phone is recorded, not sent out again or treated as a new customer request.
        await webhook.ReceiveAsync(Payload(account.InstanceName, "mobile-2", true, false, time + 60), CancellationToken.None);
        Assert.Equal(2, db.LiveSupportMessages.Count());
        Assert.Single(db.LiveSupportConversations);
        Assert.Empty(db.LiveSupportQueueEntries);
        Assert.DoesNotContain(db.LiveSupportWhatsAppMessages, item => item.Status == "Queued");
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime, db.LiveSupportWhatsAppBindings.Single().LastInboundAt);
    }

    [Fact]
    public void Phone_sent_message_is_normalized_with_recipient_and_direction()
    {
        var normalized = BaileysWebhookService.NormalizeMessage("account-a",
            Payload("account-a", "mobile-1", true, false, 1789236000).GetProperty("data"))!.Value;
        Assert.True(normalized.GetProperty("fromMe").GetBoolean());
        Assert.Equal("201099999999", normalized.GetProperty("from").GetString());
    }

    private static JsonElement Payload(string instance, string id, bool fromMe, bool history, long timestamp) =>
        JsonSerializer.SerializeToElement(new
        {
            sessionId = instance, @event = "message", history,
            data = new { key = new { id, remoteJid = "201099999999@s.whatsapp.net", fromMe },
                message = new { conversation = "رسالة اختبار" }, messageTimestamp = timestamp.ToString() }
        });

    private sealed class NoMedia : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string storagePath, CancellationToken ct) => throw new NotSupportedException();
    }
}
