using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class FacebookMessengerOutboundBackgroundServiceTests
{
    [Theory]
    [InlineData(LiveSupportSenderType.AI)]
    [InlineData(LiveSupportSenderType.System)]
    public async Task NonEmployeeMessage_IsRejectedBeforeMeta(LiveSupportSenderType senderType)
    {
        var handler = new RecordingMessengerHandler(
            "psid-1",
            failIfCalled: true);
        await using var harness = await Harness.CreateAsync(handler);
        var deliveryId = await harness.AddDeliveryAsync(
            pageId: "page-1",
            senderPsid: "psid-1",
            senderType);

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
        await using var scope = harness.Services.CreateAsyncScope();
        var delivery = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportMessengerMessages
            .SingleAsync(message => message.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("MESSENGER_HUMAN_ONLY_DISPATCH_REJECTED", delivery.FailureCode);
    }

    [Fact]
    public async Task PageTwoDelivery_UsesOnlyPageTwoTokenAndEndpoint()
    {
        var handler = new RecordingMessengerHandler("psid-2");
        await using var harness = await Harness.CreateAsync(handler);
        var deliveryId = await harness.AddDeliveryAsync(
            pageId: "page-2",
            senderPsid: "psid-2",
            LiveSupportSenderType.Staff);

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("token-2", request.AccessToken);
        Assert.Equal("https://graph.facebook.com/v25.0/page-2/messages", request.Url);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(
            "psid-2",
            body.RootElement.GetProperty("recipient").GetProperty("id").GetString());
        await using var scope = harness.Services.CreateAsyncScope();
        var delivery = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportMessengerMessages
            .SingleAsync(message => message.Id == deliveryId);
        Assert.Equal("Sent", delivery.Status);
        Assert.Equal("mid.sent", delivery.ProviderMessageId);
    }

    [Fact]
    public async Task StagedDelivery_RemainsSendableAfterConversationCloses()
    {
        var handler = new RecordingMessengerHandler("psid-1");
        await using var harness = await Harness.CreateAsync(handler);
        var deliveryId = await harness.AddDeliveryAsync(
            pageId: "page-1",
            senderPsid: "psid-1",
            LiveSupportSenderType.Staff);
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var delivery = await db.LiveSupportMessengerMessages
                .SingleAsync(message => message.Id == deliveryId);
            var conversation = await db.LiveSupportConversations
                .SingleAsync(candidate => candidate.Id == delivery.ConversationId);
            var binding = await db.LiveSupportMessengerBindings
                .SingleAsync(candidate => candidate.ConversationId == delivery.ConversationId);
            conversation.Status = LiveSupportConversationStatus.Closed;
            conversation.ClosedAt = DateTime.UtcNow;
            binding.IsOpen = false;
            await db.SaveChangesAsync();
        }

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Single(handler.Requests);
        await using var verificationScope = harness.Services.CreateAsyncScope();
        var persisted = await verificationScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportMessengerMessages.SingleAsync(message => message.Id == deliveryId);
        Assert.Equal("Sent", persisted.Status);
    }

    [Fact]
    public async Task InvalidAcceptedReceipt_IsMarkedUncertainWithoutRetry()
    {
        var handler = new InvalidReceiptHandler();
        await using var harness = await Harness.CreateAsync(handler);
        var deliveryId = await harness.AddDeliveryAsync(
            pageId: "page-1",
            senderPsid: "psid-1",
            LiveSupportSenderType.Staff);

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);
        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        await using var scope = harness.Services.CreateAsyncScope();
        var delivery = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportMessengerMessages.SingleAsync(message => message.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("MESSENGER_DELIVERY_UNCERTAIN", delivery.FailureCode);
    }

    [Fact]
    public async Task ApprovedHumanAgentPage_UsesTagAfterStandardWindow()
    {
        var handler = new RecordingMessengerHandler("psid-2");
        await using var harness = await Harness.CreateAsync(handler);
        var deliveryId = await harness.AddDeliveryAsync(
            pageId: "page-2",
            senderPsid: "psid-2",
            LiveSupportSenderType.Staff);
        await harness.SetReplyWindowAsync(
            deliveryId,
            DateTime.UtcNow.AddHours(-25),
            DateTime.UtcNow.AddDays(5));

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("MESSAGE_TAG", body.RootElement.GetProperty("messaging_type").GetString());
        Assert.Equal("HUMAN_AGENT", body.RootElement.GetProperty("tag").GetString());
    }

    [Fact]
    public async Task StandardPage_RejectsReplyAfterTwentyFourHours()
    {
        var handler = new RecordingMessengerHandler("psid-1", failIfCalled: true);
        await using var harness = await Harness.CreateAsync(handler);
        var deliveryId = await harness.AddDeliveryAsync(
            pageId: "page-1",
            senderPsid: "psid-1",
            LiveSupportSenderType.Staff);
        await harness.SetReplyWindowAsync(
            deliveryId,
            DateTime.UtcNow.AddHours(-25),
            DateTime.UtcNow.AddDays(5));

        await harness.Worker.DispatchBatchAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
        await using var scope = harness.Services.CreateAsyncScope();
        var delivery = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .LiveSupportMessengerMessages.SingleAsync(message => message.Id == deliveryId);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("MESSENGER_WINDOW_CLOSED", delivery.FailureCode);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Harness(ServiceProvider services, SqliteConnection connection)
        {
            Services = services;
            _connection = connection;
            Worker = new FacebookMessengerOutboundBackgroundService(
                services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<FacebookMessengerOutboundBackgroundService>.Instance);
        }

        public ServiceProvider Services { get; }
        public FacebookMessengerOutboundBackgroundService Worker { get; }

        public static async Task<Harness> CreateAsync(HttpMessageHandler handler)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var rawConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FacebookMessenger:VerifyToken"] = "verify-token",
                    ["FacebookMessenger:AppSecret"] = "app-secret",
                    ["FacebookMessenger:ApiVersion"] = "v25.0",
                    ["FacebookMessenger:Pages:0:PageId"] = "page-1",
                    ["FacebookMessenger:Pages:0:DisplayName"] = "صفحة أولى",
                    ["FacebookMessenger:Pages:0:AccessToken"] = "token-1",
                    ["FacebookMessenger:Pages:1:PageId"] = "page-2",
                    ["FacebookMessenger:Pages:1:DisplayName"] = "صفحة ثانية",
                    ["FacebookMessenger:Pages:1:AccessToken"] = "token-2",
                    ["FacebookMessenger:Pages:1:HumanAgentEnabled"] = "true",
                    ["FacebookMessenger:Pages:2:PageId"] = "page-3",
                    ["FacebookMessenger:Pages:2:DisplayName"] = "صفحة ثالثة",
                    ["FacebookMessenger:Pages:2:AccessToken"] = "token-3"
                })
                .Build();
            var messengerConfiguration = new FacebookMessengerConfiguration(rawConfiguration);
            var configurationReader =
                FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(messengerConfiguration);
            var mediaDownloader = new FacebookMessengerSafeMediaDownloader(
                new RejectingMediaHandler());
            var graphClient = new FacebookMessengerGraphClient(
                new HttpClient(handler),
                configurationReader,
                mediaDownloader,
                NullLogger<FacebookMessengerGraphClient>.Instance);
            var services = new ServiceCollection()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
                .AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>())
                .AddScoped<ILiveSupportEventWriter, LiveSupportEventWriter>()
                .AddSingleton(messengerConfiguration)
                .AddSingleton<IFacebookMessengerRuntimeConfigurationReader>(configurationReader)
                .AddSingleton(graphClient)
                .BuildServiceProvider();
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Database.EnsureCreatedAsync();
            return new Harness(services, connection);
        }

        public async Task<Guid> AddDeliveryAsync(
            string pageId,
            string senderPsid,
            LiveSupportSenderType senderType)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var guest = new LiveSupportGuestSession
            {
                DisplayName = "عميل ماسنجر",
                SecurityStampHash = new string('A', 64),
                CreatedIpHash = new string('B', 64),
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                LastSeenAt = DateTime.UtcNow
            };
            var conversation = new LiveSupportConversation
            {
                ParticipantType = LiveSupportParticipantType.Guest,
                GuestSessionId = guest.Id,
                Status = LiveSupportConversationStatus.Active,
                AllowsAI = false,
                LastMessageAt = DateTime.UtcNow,
                Version = 1
            };
            var senderUser = new User
            {
                FullName = "موظف الدعم",
                PhoneNumber = "01000000099",
                PasswordHash = "test-only"
            };
            var canonicalMessage = new LiveSupportMessage
            {
                ConversationId = conversation.Id,
                SenderType = senderType,
                SenderUserId = senderType is LiveSupportSenderType.Staff or LiveSupportSenderType.Admin
                    ? senderUser.Id
                    : null,
                ClientMessageId = $"staff-{Guid.NewGuid():N}",
                Type = LiveSupportMessageType.Text,
                Content = "رد بشري من الدعم",
                SentAt = DateTime.UtcNow
            };
            var binding = new LiveSupportMessengerBinding
            {
                ConversationId = conversation.Id,
                GuestSessionId = guest.Id,
                PageId = pageId,
                PageName = pageId == "page-2" ? "صفحة ثانية" : "صفحة أولى",
                SenderPsid = senderPsid,
                DisplayName = guest.DisplayName,
                IsOpen = true,
                LastInboundAt = DateTime.UtcNow.AddMinutes(-1),
                ReplyWindowExpiresAt = DateTime.UtcNow.AddHours(23),
                Version = 1
            };
            var delivery = new LiveSupportMessengerMessage
            {
                ConversationId = conversation.Id,
                LiveSupportMessageId = canonicalMessage.Id,
                PageId = pageId,
                SenderPsid = senderPsid,
                Direction = "Outbound",
                MessageType = "text",
                Status = "Pending",
                AttemptCount = 0,
                Version = 1
            };
            db.LiveSupportGuestSessions.Add(guest);
            db.LiveSupportConversations.Add(conversation);
            db.Users.Add(senderUser);
            db.LiveSupportMessages.Add(canonicalMessage);
            db.LiveSupportMessengerBindings.Add(binding);
            db.LiveSupportMessengerMessages.Add(delivery);
            await db.SaveChangesAsync();
            return delivery.Id;
        }

        public async Task SetReplyWindowAsync(
            Guid deliveryId,
            DateTime lastInboundAt,
            DateTime replyWindowExpiresAt)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var delivery = await db.LiveSupportMessengerMessages
                .SingleAsync(message => message.Id == deliveryId);
            var binding = await db.LiveSupportMessengerBindings
                .SingleAsync(candidate => candidate.ConversationId == delivery.ConversationId);
            binding.LastInboundAt = lastInboundAt;
            binding.ReplyWindowExpiresAt = replyWindowExpiresAt;
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingMessengerHandler(
        string recipientPsid,
        bool failIfCalled = false) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (failIfCalled)
                throw new InvalidOperationException("Meta must not be called for this message.");
            Requests.Add(new RecordedRequest(
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"recipient_id\":\"{recipientPsid}\",\"message_id\":\"mid.sent\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed record RecordedRequest(
        string Url,
        string? AuthorizationScheme,
        string? AccessToken,
        string Body);

    private sealed class RejectingMediaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No inbound media download expected.");
    }

    private sealed class InvalidReceiptHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
