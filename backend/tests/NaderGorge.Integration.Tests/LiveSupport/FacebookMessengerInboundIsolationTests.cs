using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class FacebookMessengerInboundIsolationTests
{
    [Fact]
    public async Task SamePsidOnTwoPages_CreatesTwoHumanOnlyConversations()
    {
        await using var harness = await InboundHarness.CreateAsync();
        using var webhook = JsonDocument.Parse("""
            {
              "object": "page",
              "entry": [
                {
                  "id": "page-1",
                  "messaging": [{
                    "sender": { "id": "shared-psid" },
                    "recipient": { "id": "page-1" },
                    "timestamp": 1788000000000,
                    "message": { "mid": "mid.page-1", "text": "رسالة للصفحة الأولى" }
                  }]
                },
                {
                  "id": "page-2",
                  "messaging": [{
                    "sender": { "id": "shared-psid" },
                    "recipient": { "id": "page-2" },
                    "timestamp": 1788000000001,
                    "message": { "mid": "mid.page-2", "text": "رسالة للصفحة الثانية" }
                  }]
                }
              ]
            }
            """);

        await harness.IngestAsync(webhook.RootElement);

        harness.Db.ChangeTracker.Clear();
        var bindings = await harness.Db.LiveSupportMessengerBindings
            .OrderBy(binding => binding.PageId)
            .ToListAsync();
        Assert.Equal(["page-1", "page-2"], bindings.Select(binding => binding.PageId));
        Assert.Equal(2, bindings.Select(binding => binding.ConversationId).Distinct().Count());
        Assert.Equal(2, bindings.Select(binding => binding.GuestSessionId).Distinct().Count());
        var conversationIds = bindings.Select(binding => binding.ConversationId).ToArray();
        var conversations = await harness.Db.LiveSupportConversations
            .Where(conversation => conversationIds.Contains(conversation.Id))
            .ToListAsync();
        Assert.All(conversations, conversation => Assert.False(conversation.AllowsAI));
        Assert.Equal(2, await harness.Db.LiveSupportMessengerMessages.CountAsync());
        Assert.False(await harness.Db.LiveSupportAIConversationStates.AnyAsync());
        Assert.False(await harness.Db.LiveSupportAITurns.AnyAsync());

        var dashboard = await new LiveSupportService(harness.Db, new EnabledSettings())
            .GetAdminDashboardAsync(CancellationToken.None);
        var messengerRows = dashboard.Conversations
            .OrderBy(row => row.ExternalPageId)
            .ToArray();
        Assert.Equal(["page-1", "page-2"], messengerRows.Select(row => row.ExternalPageId));
        Assert.Equal(["صفحة أولى", "صفحة ثانية"], messengerRows.Select(row => row.ExternalPageName));
        Assert.All(messengerRows, row =>
        {
            Assert.Equal("Messenger", row.Channel);
            Assert.Equal("Received", row.LastExternalDeliveryStatus);
            Assert.Null(row.ExternalPhoneNumber);
        });
    }

    [Fact]
    public async Task Regression_2026_08_31_WatermarkReceipt_DoesNotConsumePendingDelivery()
    {
        await using var harness = await InboundHarness.CreateAsync();
        using var inbound = JsonDocument.Parse(SingleMessageWebhook());
        await harness.IngestAsync(inbound.RootElement);
        var deliveryId = await harness.AddPendingDeliveryAsync();
        var watermark = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();
        using var receipt = JsonDocument.Parse($$"""
            {
              "object": "page",
              "entry": [{
                "id": "page-1",
                "messaging": [{
                  "sender": { "id": "shared-psid" },
                  "recipient": { "id": "page-1" },
                  "timestamp": {{watermark}},
                  "delivery": { "watermark": {{watermark}} }
                }]
              }]
            }
            """);

        await harness.IngestAsync(receipt.RootElement);

        harness.Db.ChangeTracker.Clear();
        var delivery = await harness.Db.LiveSupportMessengerMessages
            .SingleAsync(message => message.Id == deliveryId);
        Assert.Equal("Pending", delivery.Status);
        Assert.Null(delivery.ProviderMessageId);
    }

    private static string SingleMessageWebhook() => """
        {
          "object": "page",
          "entry": [{
            "id": "page-1",
            "messaging": [{
              "sender": { "id": "shared-psid" },
              "recipient": { "id": "page-1" },
              "timestamp": 1788000000000,
              "message": { "mid": "mid.initial", "text": "رسالة أولى" }
            }]
          }]
        }
        """;

    private sealed class InboundHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly FacebookMessengerSafeMediaDownloader _downloader;
        private readonly FacebookMessengerLiveSupportService _service;

        private InboundHarness(
            SqliteConnection connection,
            AppDbContext db,
            FacebookMessengerSafeMediaDownloader downloader,
            FacebookMessengerLiveSupportService service)
        {
            _connection = connection;
            _downloader = downloader;
            _service = service;
            Db = db;
        }

        public AppDbContext Db { get; }

        public static async Task<InboundHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var configuration = Configuration();
            var configurationReader =
                FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(configuration);
            var support = new LiveSupportService(db, new EnabledSettings());
            var downloader = new FacebookMessengerSafeMediaDownloader(new RejectingHandler());
            var graph = new FacebookMessengerGraphClient(
                new HttpClient(new ProfileHandler()),
                configurationReader,
                downloader,
                NullLogger<FacebookMessengerGraphClient>.Instance);
            var service = new FacebookMessengerLiveSupportService(
                db,
                support,
                support,
                new RejectingStorage(),
                configurationReader,
                new FacebookMessengerWebhookParser(configuration),
                graph);
            return new InboundHarness(connection, db, downloader, service);
        }

        public async Task IngestAsync(JsonElement webhook)
        {
            var enqueued = await _service.EnqueueWebhookAsync(webhook, CancellationToken.None);
            var inboxIds = await Db.LiveSupportMessengerWebhookInbox
                .Where(inbox => inbox.Status == "Pending")
                .OrderBy(inbox => inbox.CreatedAt)
                .ThenBy(inbox => inbox.Id)
                .Select(inbox => inbox.Id)
                .ToListAsync();
            Assert.Equal(enqueued, inboxIds.Count);
            foreach (var inboxId in inboxIds)
            {
                var inbox = await Db.LiveSupportMessengerWebhookInbox
                    .SingleAsync(candidate => candidate.Id == inboxId);
                inbox.Status = "Processing";
                inbox.ClaimedAt = DateTime.UtcNow;
                inbox.AttemptCount = 1;
                await Db.SaveChangesAsync();
                await _service.ProcessInboxEventAsync(inboxId, CancellationToken.None);
            }
        }

        public async Task<Guid> AddPendingDeliveryAsync()
        {
            var binding = await Db.LiveSupportMessengerBindings
                .SingleAsync(candidate => candidate.PageId == "page-1");
            var delivery = new LiveSupportMessengerMessage
            {
                ConversationId = binding.ConversationId,
                PageId = binding.PageId,
                SenderPsid = binding.SenderPsid,
                Direction = "Outbound",
                MessageType = "text",
                Status = "Pending",
                Version = 1
            };
            Db.LiveSupportMessengerMessages.Add(delivery);
            await Db.SaveChangesAsync();
            return delivery.Id;
        }

        public async ValueTask DisposeAsync()
        {
            _downloader.Dispose();
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static FacebookMessengerConfiguration Configuration()
        {
            var values = new Dictionary<string, string?>
            {
                ["FacebookMessenger:VerifyToken"] = "verify-token",
                ["FacebookMessenger:AppSecret"] = "app-secret",
                ["FacebookMessenger:ApiVersion"] = "v25.0",
                ["FacebookMessenger:Pages:0:PageId"] = "page-1",
                ["FacebookMessenger:Pages:0:DisplayName"] = "صفحة أولى",
                ["FacebookMessenger:Pages:0:AccessToken"] = "token-1",
                ["FacebookMessenger:Pages:1:PageId"] = "page-2",
                ["FacebookMessenger:Pages:1:DisplayName"] = "صفحة ثانية",
                ["FacebookMessenger:Pages:1:AccessToken"] = "token-2"
            };
            return new FacebookMessengerConfiguration(
                new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        }
    }

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate() { }
    }

    private sealed class ProfileHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"first_name\":\"عميل\",\"last_name\":\"مشترك\"}",
                    Encoding.UTF8,
                    "application/json")
            });
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No media request expected.");
    }

    private sealed class RejectingStorage : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(
            Stream content,
            string fileName,
            string contentType,
            long sizeBytes,
            CancellationToken ct) =>
            throw new InvalidOperationException("No attachment storage call expected.");

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No attachment read expected.");

        public Task DeleteAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No attachment deletion expected.");
    }
}
