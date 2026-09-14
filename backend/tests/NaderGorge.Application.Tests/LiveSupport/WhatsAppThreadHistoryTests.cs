using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class WhatsAppThreadHistoryTests
{
    private const string WhatsAppUserId = "201099999999";

    [Fact]
    public async Task ClosedContact_NewInboundLinksAndReturnsEnrichedContinuousThread()
    {
        await using var scenario = await SqliteScenario.CreateAsync();
        var oldestConversation = await SeedClosedWhatsAppConversationAsync(scenario, WhatsAppUserId);
        oldestConversation.ClosedAt = DateTime.UtcNow.AddDays(-2);
        await scenario.Db.SaveChangesAsync();
        var oldConversation = await SeedOwnedWhatsAppConversationAsync(scenario, WhatsAppUserId, LiveSupportTestData.StaffAId);
        var oldInbound = Message(oldConversation.Id, "قديم وارد", LiveSupportSenderType.Guest, DateTime.UtcNow.AddHours(-2));
        var oldOutbound = Message(oldConversation.Id, "قديم صادر", LiveSupportSenderType.Staff, DateTime.UtcNow.AddHours(-1));
        oldInbound.SenderGuestSessionId = oldConversation.GuestSessionId;
        oldOutbound.SenderUserId = LiveSupportTestData.StaffAId;
        scenario.Db.LiveSupportMessages.AddRange(oldInbound, oldOutbound);
        scenario.Db.LiveSupportWhatsAppMessages.AddRange(
            Delivery(oldConversation.Id, oldInbound.Id, "Inbound", "Received"),
            Delivery(oldConversation.Id, oldOutbound.Id, "Outbound", "Read"));
        await scenario.Db.SaveChangesAsync();
        await scenario.Support.CloseAsync(LiveSupportTestData.StaffAId, false, oldConversation.Id, "تم الحل", CancellationToken.None);

        using var webhook = JsonDocument.Parse(InboundWebhook("wamid.follow-up", WhatsAppUserId, "رسالة متابعة"));
        await scenario.WhatsApp.ProcessWebhookAsync(webhook.RootElement, CancellationToken.None);
        var currentConversation = await scenario.Db.LiveSupportConversations.SingleAsync(conversation =>
            conversation.Status != LiveSupportConversationStatus.Closed &&
            conversation.Status != LiveSupportConversationStatus.Abandoned);
        currentConversation.CurrentOwnerUserId = LiveSupportTestData.StaffAId;
        currentConversation.Status = LiveSupportConversationStatus.Assigned;
        await scenario.Db.SaveChangesAsync();

        Assert.Equal(oldConversation.Id, currentConversation.PreviousConversationId);
        Assert.Null(currentConversation.LinkedStudentUserId);
        var exactMessages = await scenario.Support.GetStaffMessagesAsync(
            LiveSupportTestData.StaffAId, false, currentConversation.Id, 100, CancellationToken.None);
        Assert.Equal("رسالة متابعة", Assert.Single(exactMessages).Content);

        var thread = await scenario.Support.GetStaffWhatsAppThreadAsync(
            ThreadQuery(LiveSupportTestData.StaffAId, currentConversation.Id, 100), CancellationToken.None);
        Assert.Equal(["قديم وارد", "قديم صادر", "رسالة متابعة"], thread.Items.Select(message => message.Content).ToArray());
        var enrichedOutbound = Assert.Single(thread.Items, message => message.Id == oldOutbound.Id);
        Assert.Equal("Read", enrichedOutbound.ExternalDeliveryStatus);
        Assert.Equal("موظف أول", enrichedOutbound.SenderDisplayName);
        var currentInbound = Assert.Single(thread.Items, message => message.Content == "رسالة متابعة");
        Assert.Equal("Received", currentInbound.ExternalDeliveryStatus);
    }

    [Fact]
    public async Task WhatsAppThread_RejectsStaffWithoutCurrentOwnership()
    {
        await using var scenario = await SqliteScenario.CreateAsync();
        var currentConversation = await SeedOwnedWhatsAppConversationAsync(scenario, WhatsAppUserId, LiveSupportTestData.StaffAId);

        var failure = await Assert.ThrowsAsync<LiveSupportException>(() =>
            scenario.Support.GetStaffWhatsAppThreadAsync(
                ThreadQuery(LiveSupportTestData.StaffBId, currentConversation.Id, 50), CancellationToken.None));

        Assert.Equal(LiveSupportErrorCodes.Forbidden, failure.Code);
    }

    [Fact]
    public async Task WhatsAppThread_PaginatesMoreThanHundredMessagesWithoutGapsOrDuplicates()
    {
        await using var scenario = await SqliteScenario.CreateAsync();
        var oldConversation = await SeedClosedWhatsAppConversationAsync(scenario, WhatsAppUserId);
        var currentConversation = await SeedOwnedWhatsAppConversationAsync(scenario, WhatsAppUserId, LiveSupportTestData.StaffAId);
        var sentAt = DateTime.UtcNow.AddDays(-1);
        var expectedIds = Enumerable.Range(1, 151).Select(DeterministicGuid).ToArray();
        scenario.Db.LiveSupportMessages.AddRange(expectedIds.Select((id, index) => new LiveSupportMessage
        {
            Id = id,
            ConversationId = index < 80 ? oldConversation.Id : currentConversation.Id,
            SenderType = LiveSupportSenderType.Guest,
            SenderGuestSessionId = index < 80 ? oldConversation.GuestSessionId : currentConversation.GuestSessionId,
            ClientMessageId = $"history-{index}",
            Type = LiveSupportMessageType.Text,
            Content = $"رسالة {index}",
            SentAt = sentAt
        }));
        await scenario.Db.SaveChangesAsync();

        var loadedIds = new HashSet<Guid>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 10; pageNumber++)
        {
            var page = await scenario.Support.GetStaffWhatsAppThreadAsync(
                ThreadQuery(LiveSupportTestData.StaffAId, currentConversation.Id, 37, cursor), CancellationToken.None);
            Assert.True(page.Items.All(message => loadedIds.Add(message.Id)));
            cursor = page.NextCursor;
            if (cursor is null) break;
        }

        Assert.Null(cursor);
        Assert.Equal(expectedIds.OrderBy(id => id).ToArray(), loadedIds.OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task WhatsAppThread_ExcludesAnotherOpenConversationWithSameWhatsAppIdentity()
    {
        await using var scenario = await SqliteScenario.CreateAsync();
        var currentConversation = await SeedOwnedWhatsAppConversationAsync(
            scenario, WhatsAppUserId, LiveSupportTestData.StaffAId);
        var otherOpenConversation = await SeedOwnedWhatsAppConversationAsync(
            scenario, WhatsAppUserId, LiveSupportTestData.StaffBId);
        var otherStaffMessage = Message(
            otherOpenConversation.Id, "رسالة لموظف آخر", LiveSupportSenderType.Staff, DateTime.UtcNow);
        otherStaffMessage.SenderUserId = LiveSupportTestData.StaffBId;
        scenario.Db.LiveSupportMessages.Add(otherStaffMessage);
        await scenario.Db.SaveChangesAsync();
        var otherAttachment = await SeedAttachmentAsync(scenario, otherOpenConversation.Id, "private");

        var thread = await scenario.Support.GetStaffWhatsAppThreadAsync(
            ThreadQuery(LiveSupportTestData.StaffAId, currentConversation.Id, 100), CancellationToken.None);

        Assert.DoesNotContain(thread.Items, message => message.ConversationId == otherOpenConversation.Id);
        var attachmentFailure = await Assert.ThrowsAsync<LiveSupportException>(() =>
            scenario.Support.OpenStaffWhatsAppThreadAttachmentAsync(
                AttachmentQuery(LiveSupportTestData.StaffAId, currentConversation.Id, otherAttachment.Id), CancellationToken.None));
        Assert.Equal("NOT_FOUND", attachmentFailure.Code);
    }

    [Fact]
    public async Task WhatsAppThread_AdminCanUseTerminalAnchorAcrossAllExactIdentityEpisodes()
    {
        await using var scenario = await SqliteScenario.CreateAsync();
        var terminalConversation = await SeedClosedWhatsAppConversationAsync(scenario, WhatsAppUserId);
        var activeConversation = await SeedOwnedWhatsAppConversationAsync(
            scenario, WhatsAppUserId, LiveSupportTestData.StaffBId);
        var terminalMessage = Message(
            terminalConversation.Id, "رسالة مغلقة", LiveSupportSenderType.Guest, DateTime.UtcNow.AddHours(-2));
        terminalMessage.SenderGuestSessionId = terminalConversation.GuestSessionId;
        var activeMessage = Message(
            activeConversation.Id, "رسالة حالية", LiveSupportSenderType.Guest, DateTime.UtcNow.AddHours(-1));
        activeMessage.SenderGuestSessionId = activeConversation.GuestSessionId;
        scenario.Db.LiveSupportMessages.AddRange(terminalMessage, activeMessage);
        await scenario.Db.SaveChangesAsync();
        var activeAttachment = await SeedAttachmentAsync(scenario, activeConversation.Id, "admin-history");

        var thread = await scenario.Support.GetStaffWhatsAppThreadAsync(
            new LiveSupportStaffWhatsAppThreadQuery(
                LiveSupportTestData.StaffAId, true, terminalConversation.Id, 100, null),
            CancellationToken.None);

        Assert.Contains(thread.Items, message => message.Id == terminalMessage.Id);
        Assert.Contains(thread.Items, message => message.Id == activeMessage.Id);
        var opened = await scenario.Support.OpenStaffWhatsAppThreadAttachmentAsync(
            new LiveSupportStaffWhatsAppAttachmentQuery(
                LiveSupportTestData.StaffAId, true, terminalConversation.Id, activeAttachment.Id),
            CancellationToken.None);
        await using var openedContent = opened.Content;
        using var reader = new StreamReader(openedContent);
        Assert.Equal("admin-history", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task HistoricalAttachment_CurrentOwnerCanOpenOnlySameWhatsAppContact()
    {
        await using var scenario = await SqliteScenario.CreateAsync();
        var oldConversation = await SeedClosedWhatsAppConversationAsync(scenario, WhatsAppUserId);
        var currentConversation = await SeedOwnedWhatsAppConversationAsync(scenario, WhatsAppUserId, LiveSupportTestData.StaffAId);
        var otherConversation = await SeedClosedWhatsAppConversationAsync(scenario, "201088888888");
        var allowedAttachment = await SeedAttachmentAsync(scenario, oldConversation.Id, "allowed");
        var otherAttachment = await SeedAttachmentAsync(scenario, otherConversation.Id, "other");

        var opened = await scenario.Support.OpenStaffWhatsAppThreadAttachmentAsync(
            AttachmentQuery(LiveSupportTestData.StaffAId, currentConversation.Id, allowedAttachment.Id), CancellationToken.None);
        await using var openedContent = opened.Content;
        using var reader = new StreamReader(openedContent);
        Assert.Equal("allowed", await reader.ReadToEndAsync());

        var otherContactFailure = await Assert.ThrowsAsync<LiveSupportException>(() =>
            scenario.Support.OpenStaffWhatsAppThreadAttachmentAsync(
                AttachmentQuery(LiveSupportTestData.StaffAId, currentConversation.Id, otherAttachment.Id), CancellationToken.None));
        Assert.Equal("NOT_FOUND", otherContactFailure.Code);
    }

    [Fact]
    public async Task HistoricalAttachment_RejectsStaffWithoutCurrentOwnership()
    {
        await using var scenario = await SqliteScenario.CreateAsync();
        var oldConversation = await SeedClosedWhatsAppConversationAsync(scenario, WhatsAppUserId);
        var currentConversation = await SeedOwnedWhatsAppConversationAsync(scenario, WhatsAppUserId, LiveSupportTestData.StaffAId);
        var allowedAttachment = await SeedAttachmentAsync(scenario, oldConversation.Id, "allowed");

        var ownershipFailure = await Assert.ThrowsAsync<LiveSupportException>(() =>
            scenario.Support.OpenStaffWhatsAppThreadAttachmentAsync(
                AttachmentQuery(LiveSupportTestData.StaffBId, currentConversation.Id, allowedAttachment.Id), CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, ownershipFailure.Code);
    }

    private static LiveSupportStaffWhatsAppThreadQuery ThreadQuery(Guid staffId, Guid conversationId, int pageSize, string? cursor = null) =>
        new(staffId, false, conversationId, pageSize, cursor);

    private static LiveSupportStaffWhatsAppAttachmentQuery AttachmentQuery(Guid staffId, Guid conversationId, Guid attachmentId) =>
        new(staffId, false, conversationId, attachmentId);

    private static async Task<LiveSupportConversation> SeedOwnedWhatsAppConversationAsync(
        SqliteScenario scenario,
        string whatsAppUserId,
        Guid ownerId)
    {
        var guest = Guest(whatsAppUserId);
        var conversation = Conversation(guest.Id, LiveSupportConversationStatus.Assigned, ownerId);
        scenario.Db.LiveSupportGuestSessions.Add(guest);
        scenario.Db.LiveSupportConversations.Add(conversation);
        scenario.Db.LiveSupportWhatsAppBindings.Add(Binding(conversation.Id, guest, whatsAppUserId));
        scenario.Db.LiveSupportAssignments.Add(new LiveSupportAssignment
        {
            ConversationId = conversation.Id,
            StaffUserId = ownerId,
            StartedAt = DateTime.UtcNow,
            AssignmentSequence = 1
        });
        await scenario.Db.SaveChangesAsync();
        return conversation;
    }

    private static async Task<LiveSupportConversation> SeedClosedWhatsAppConversationAsync(SqliteScenario scenario, string whatsAppUserId)
    {
        var guest = Guest(whatsAppUserId);
        var conversation = Conversation(guest.Id, LiveSupportConversationStatus.Closed, null);
        conversation.ClosedAt = DateTime.UtcNow.AddHours(-1);
        scenario.Db.LiveSupportGuestSessions.Add(guest);
        scenario.Db.LiveSupportConversations.Add(conversation);
        scenario.Db.LiveSupportWhatsAppBindings.Add(Binding(conversation.Id, guest, whatsAppUserId));
        await scenario.Db.SaveChangesAsync();
        return conversation;
    }

    private static async Task<LiveSupportAttachment> SeedAttachmentAsync(SqliteScenario scenario, Guid conversationId, string content)
    {
        var attachment = new LiveSupportAttachment
        {
            StoragePath = $"history/{Guid.NewGuid():N}",
            OriginalFileName = "history.txt",
            ContentType = "text/plain",
            SizeBytes = content.Length,
            Sha256 = new string('a', 64),
            UploadedByIdentity = LiveSupportTestData.StaffAId.ToString("N")
        };
        scenario.Storage.Add(attachment.StoragePath, content);
        scenario.Db.LiveSupportAttachments.Add(attachment);
        scenario.Db.LiveSupportMessages.Add(new LiveSupportMessage
        {
            ConversationId = conversationId,
            SenderType = LiveSupportSenderType.Staff,
            SenderUserId = LiveSupportTestData.StaffAId,
            ClientMessageId = Guid.NewGuid().ToString("N"),
            Type = LiveSupportMessageType.Pdf,
            Content = attachment.OriginalFileName,
            AttachmentId = attachment.Id,
            SentAt = DateTime.UtcNow.AddMinutes(-30)
        });
        await scenario.Db.SaveChangesAsync();
        return attachment;
    }

    private static LiveSupportGuestSession Guest(string whatsAppUserId) => new()
    {
        DisplayName = $"عميل {whatsAppUserId[^4..]}",
        PhoneNumber = whatsAppUserId.StartsWith("20", StringComparison.Ordinal) ? $"0{whatsAppUserId[2..]}" : whatsAppUserId,
        SecurityStampHash = new string('a', 64),
        CreatedIpHash = new string('b', 64),
        ExpiresAt = DateTime.UtcNow.AddYears(1),
        LastSeenAt = DateTime.UtcNow
    };

    private static LiveSupportConversation Conversation(Guid guestId, LiveSupportConversationStatus status, Guid? ownerId) => new()
    {
        ParticipantType = LiveSupportParticipantType.Guest,
        GuestSessionId = guestId,
        Status = status,
        CurrentOwnerUserId = ownerId,
        QueuedAt = DateTime.UtcNow.AddHours(-2),
        LastMessageAt = DateTime.UtcNow.AddHours(-1),
        Version = 1
    };

    private static LiveSupportWhatsAppBinding Binding(Guid conversationId, LiveSupportGuestSession guest, string whatsAppUserId) => new()
    {
        ConversationId = conversationId,
        GuestSessionId = guest.Id,
        WhatsAppUserId = whatsAppUserId,
        PhoneNumber = guest.PhoneNumber,
        DisplayName = guest.DisplayName,
        LastInboundAt = DateTime.UtcNow.AddHours(-1),
        CustomerServiceWindowExpiresAt = DateTime.UtcNow.AddHours(23),
        Version = 1
    };

    private static LiveSupportMessage Message(Guid conversationId, string content, LiveSupportSenderType senderType, DateTime sentAt) => new()
    {
        ConversationId = conversationId,
        SenderType = senderType,
        ClientMessageId = Guid.NewGuid().ToString("N"),
        Type = LiveSupportMessageType.Text,
        Content = content,
        SentAt = sentAt
    };

    private static LiveSupportWhatsAppMessage Delivery(Guid conversationId, Guid messageId, string direction, string status) => new()
    {
        ConversationId = conversationId,
        LiveSupportMessageId = messageId,
        MetaMessageId = $"wamid.{messageId:N}",
        Direction = direction,
        MessageType = "text",
        Status = status,
        Version = 1
    };

    private static Guid DeterministicGuid(int sequence) =>
        Guid.Parse($"00000000-0000-0000-0000-{sequence:D12}");

    private static string InboundWebhook(string messageId, string whatsAppUserId, string text) => $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "business-id",
            "changes": [{
              "value": {
                "metadata": { "phone_number_id": "phone-id" },
                "contacts": [{ "wa_id": "{{whatsAppUserId}}", "profile": { "name": "عميل واتساب" } }],
                "messages": [{
                  "id": "{{messageId}}",
                  "from": "{{whatsAppUserId}}",
                  "timestamp": "1787529600",
                  "type": "text",
                  "text": { "body": "{{text}}" }
                }]
              }
            }]
          }]
        }
        """;

    private sealed record SqliteScenario(
        SqliteConnection Connection,
        AppDbContext Db,
        LiveSupportService Support,
        WhatsAppLiveSupportService WhatsApp,
        TestAttachmentStorage Storage) : IAsyncDisposable
    {
        public static async Task<SqliteScenario> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(
                LiveSupportTestData.User(LiveSupportTestData.StaffAId, "موظف أول", "01011111111"),
                LiveSupportTestData.User(LiveSupportTestData.StaffBId, "موظف ثان", "01022222222"));
            await db.SaveChangesAsync();
            var storage = new TestAttachmentStorage();
            var support = new LiveSupportService(db, new LiveSupportEnabledSettings(), attachmentStorage: storage);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsAppCloudApi:BusinessAccountId"] = "business-id",
                ["WhatsAppCloudApi:PhoneNumberId"] = "phone-id"
            }).Build();
            var cloud = new WhatsAppCloudService(new HttpClient(), configuration, NullLogger<WhatsAppCloudService>.Instance);
            var whatsApp = new WhatsAppLiveSupportService(
                db, support, storage, cloud, new LiveSupportEventWriter(db), configuration, new StubWhatsAppCampaignService());
            return new SqliteScenario(connection, db, support, whatsApp, storage);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class TestAttachmentStorage : ILiveSupportAttachmentStorage
    {
        private readonly Dictionary<string, byte[]> _contentByPath = [];

        public void Add(string storagePath, string content) =>
            _contentByPath[storagePath] = System.Text.Encoding.UTF8.GetBytes(content);

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(_contentByPath[storagePath], writable: false));

        public Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storagePath, CancellationToken ct) => throw new NotSupportedException();
    }
}
