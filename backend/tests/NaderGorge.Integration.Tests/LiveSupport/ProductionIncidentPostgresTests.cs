using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Auth.Commands;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class ProductionIncidentPostgresTests
{
    [Theory]
    [InlineData(0, "correct", true)]
    [InlineData(1, "correct", false)]
    [InlineData(0, "incorrect", false)]
    public async Task Incident20260906_RemovedDeviceLogin_ReusesIdentityOnlyAfterPasswordAndLimitChecks(
        int activeDevices, string password, bool allowed)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var studentRole = await fixture.Db.Roles.FirstAsync(role => role.Name == "Student");
        var user = new User
        {
            FullName = "Integration login incident",
            PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct", 4),
            IsActive = true
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = studentRole.Id });
        var removed = new Device { UserId = user.Id, DeviceFingerprint = "removed", IsActive = false };
        user.Devices.Add(removed);
        for (var index = 0; index < activeDevices; index++)
            user.Devices.Add(new Device { UserId = user.Id, DeviceFingerprint = $"active-{index}" });
        fixture.Db.Users.Add(user);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new LoginCommandHandler(fixture.Db, new TokenService(Configuration()), new Settings());
        var request = new LoginCommand(user.PhoneNumber, password, "removed", "integration", "127.0.0.1", studentRole.AllowedDomain);

        if (allowed)
        {
            var response = await handler.Handle(request, CancellationToken.None);
            Assert.NotNull(response.Data);
        }
        else if (password == "incorrect")
        {
            var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(request, CancellationToken.None));
            Assert.Equal("Invalid phone number or password", error.Message);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(request, CancellationToken.None));
            Assert.Contains("للحد الأقصى للأجهزة", error.Message);
        }

        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.Devices.SingleAsync(device => device.Id == removed.Id);
        Assert.Equal(allowed, persisted.IsActive);
        Assert.Equal(activeDevices + 1, await fixture.Db.Devices.CountAsync(device => device.UserId == user.Id));
        Assert.Equal(allowed ? 1 : 0, await fixture.Db.RefreshTokens.CountAsync(token => token.UserId == user.Id));
    }

    [Fact]
    public async Task Incident20260906_BatchedEvents_CommitDistinctSequencesAndMatchingOutbox()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var conversation = await SeedConversationAsync(fixture.Db);
        var writer = new LiveSupportEventWriter(fixture.Db);
        var request = new LiveSupportEventWriteRequest(conversation.Id, LiveSupportEventType.WhatsAppDeliveryStatusChanged);

        var first = await writer.AppendAsync(request, CancellationToken.None);
        var second = await writer.AppendAsync(request, CancellationToken.None);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        Assert.Equal(first + 1, second);
        Assert.Equal(new[] { first, second }, await fixture.Db.LiveSupportEvents
            .Where(item => item.ConversationId == conversation.Id).OrderBy(item => item.Sequence)
            .Select(item => item.Sequence).ToArrayAsync());
        var payloads = await fixture.Db.OutboxEvents.Select(item => item.PayloadJson).ToArrayAsync();
        Assert.Equal(4, payloads.Length);
        Assert.All(payloads, payload =>
        {
            using var json = JsonDocument.Parse(payload);
            Assert.Contains(json.RootElement.GetProperty("sequence").GetInt64(), new[] { first, second });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Incident20260906_ConcurrentDeliveryReceipts_ConvergeWithoutDuplicateEvents(bool campaignDelivery)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var conversation = await SeedConversationAsync(fixture.Db);
        fixture.Db.LiveSupportWhatsAppMessages.Add(new LiveSupportWhatsAppMessage
        {
            ConversationId = conversation.Id, MetaMessageId = "wamid.incident", Status = "Sent", Version = 1
        });
        await fixture.Db.SaveChangesAsync();
        Guid? campaignId = campaignDelivery ? await SeedCampaignAsync(fixture.Db) : null;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statuses = new[] { "sent", "delivered", "read", "read", "delivered", "sent" };
        var tasks = statuses.Select(async status =>
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(fixture.ConnectionString).Options);
            var configuration = Configuration();
            using var client = new HttpClient();
            var cloud = new WhatsAppCloudService(client, configuration, NullLogger<WhatsAppCloudService>.Instance);
            var campaigns = new WhatsAppCampaignService(db,
                new WhatsAppCampaignDataProtector(new EphemeralDataProtectionProvider(), configuration), configuration);
            var service = new WhatsAppLiveSupportService(db, new LiveSupportService(db, new Settings()),
                new NoAttachmentStorage(), cloud, new LiveSupportEventWriter(db), configuration, campaigns);
            using var payload = JsonDocument.Parse($$$"""
                {"object":"whatsapp_business_account","entry":[{"id":"business-id","changes":[{"value":{"metadata":{"phone_number_id":"phone-id"},"statuses":[
                  {"id":"wamid.incident","status":"{{{status}}}","timestamp":"1788680000"}
                ]}}]}]}
                """);
            await start.Task;
            await service.ProcessWebhookAsync(payload.RootElement, CancellationToken.None);
        }).ToArray();
        start.SetResult();

        await Task.WhenAll(tasks);

        fixture.Db.ChangeTracker.Clear();
        var delivery = await fixture.Db.LiveSupportWhatsAppMessages.SingleAsync();
        Assert.Equal("Read", delivery.Status);
        Assert.NotNull(delivery.ReadAt);
        Assert.NotNull(delivery.DeliveredAt);
        var events = await fixture.Db.LiveSupportEvents.Where(item => item.ConversationId == conversation.Id).ToArrayAsync();
        Assert.InRange(events.Length, 1, 3);
        Assert.Equal(events.Length, events.Select(item => item.Sequence).Distinct().Count());
        Assert.Equal(events.Length * 2, await fixture.Db.OutboxEvents.CountAsync(item => item.Type == "LiveSupportEvent"));
        if (campaignId.HasValue)
        {
            var campaign = await fixture.Db.WhatsAppCampaigns.SingleAsync(item => item.Id == campaignId);
            Assert.Equal(WhatsAppCampaignStatus.Completed, campaign.Status);
            Assert.Equal(1, campaign.ReadCount);
            Assert.Equal(0, campaign.PendingCount);
            Assert.Equal(WhatsAppCampaignRecipientStatus.Read,
                (await fixture.Db.WhatsAppCampaignRecipients.SingleAsync(item => item.CampaignId == campaignId)).Status);
        }
    }

    [Fact]
    public async Task Incident20260906_CloseAfterConcurrentUpdate_RereadsAndCommitsOneCloseEvent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var conversation = await SeedConversationAsync(fixture.Db);
        var admin = new User
        {
            FullName = "Integration administrator", PasswordHash = "not-used",
            PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}"
        };
        fixture.Db.Users.Add(admin);
        await fixture.Db.SaveChangesAsync();
        // Keep the tracked version stale to reproduce a receipt/message arriving during close.
        await fixture.Db.LiveSupportConversations.Where(item => item.Id == conversation.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Version, item => item.Version + 1));
        var service = new LiveSupportService(fixture.Db, new Settings());

        await service.CloseAsync(admin.Id, true, conversation.Id, "Integration close", CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var closed = await fixture.Db.LiveSupportConversations.SingleAsync(item => item.Id == conversation.Id);
        Assert.Equal(LiveSupportConversationStatus.Closed, closed.Status);
        Assert.Equal(3, closed.Version);
        Assert.Single(await fixture.Db.LiveSupportEvents.Where(item => item.ConversationId == conversation.Id
            && item.Type == LiveSupportEventType.Closed).ToArrayAsync());
    }

    private static async Task<Guid> SeedCampaignAsync(AppDbContext db)
    {
        var creator = new User
        {
            FullName = "Integration campaign owner", PasswordHash = "not-used",
            PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}"
        };
        var template = new LiveSupportWhatsAppTemplate
        {
            Name = $"incident_{Guid.NewGuid():N}", MetaTemplateId = Guid.NewGuid().ToString("N"),
            Language = "ar", Category = "UTILITY", Status = "APPROVED"
        };
        var campaign = new WhatsAppCampaign
        {
            Name = "Integration receipts", CreatedByUserId = creator.Id, TemplateId = template.Id,
            Status = WhatsAppCampaignStatus.Running, RecipientCount = 1, SentCount = 1, Version = 1,
            CreateIdempotencyKey = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(creator);
        db.LiveSupportWhatsAppTemplates.Add(template);
        db.WhatsAppCampaigns.Add(campaign);
        db.WhatsAppCampaignRecipients.Add(new WhatsAppCampaignRecipient
        {
            CampaignId = campaign.Id, MetaMessageId = "wamid.incident", DestinationHash = new string('d', 64),
            Status = WhatsAppCampaignRecipientStatus.Sent, Version = 1
        });
        await db.SaveChangesAsync();
        return campaign.Id;
    }

    private static async Task<LiveSupportConversation> SeedConversationAsync(AppDbContext db)
    {
        var guest = new LiveSupportGuestSession
        {
            DisplayName = "Integration incident", PhoneNumber = "01099999999",
            SecurityStampHash = new string('a', 64), CreatedIpHash = new string('b', 64),
            ExpiresAt = DateTime.UtcNow.AddHours(1), LastSeenAt = DateTime.UtcNow
        };
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest, GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting, QueuedAt = DateTime.UtcNow, Version = 1
        };
        db.LiveSupportGuestSessions.Add(guest);
        db.LiveSupportConversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation;
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = new string('s', 64),
            ["WhatsAppCloudApi:BusinessAccountId"] = "business-id",
            ["WhatsAppCloudApi:PhoneNumberId"] = "phone-id",
            ["WhatsAppCampaigns:HmacKey"] = Convert.ToBase64String(new byte[32])
        }).Build();

    private sealed class Settings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { MaxActiveDevicesPerStudent = 1, LiveSupportEnabled = true });
        public void Invalidate() { }
    }

    private sealed class NoAttachmentStorage : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(Stream content, string name, string type, long size, CancellationToken ct) =>
            throw new InvalidOperationException("Receipts must not upload media.");
        public Task<Stream> OpenReadAsync(string path, CancellationToken ct) => throw new InvalidOperationException();
        public Task DeleteAsync(string path, CancellationToken ct) => throw new InvalidOperationException();
    }
}
