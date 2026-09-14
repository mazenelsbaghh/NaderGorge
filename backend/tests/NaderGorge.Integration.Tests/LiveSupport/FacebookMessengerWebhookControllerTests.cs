using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class FacebookMessengerWebhookControllerTests
{
    [Fact]
    public async Task VerificationChallenge_RequiresExactConfiguredToken()
    {
        var configurationReader =
            FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(Configuration());
        var controller = new FacebookMessengerLiveSupportController(configurationReader, null!);

        var accepted = Assert.IsType<ContentResult>(
            await controller.VerifyWebhook(
                "subscribe", "verify-token", "challenge-123", CancellationToken.None));
        var rejected = await controller.VerifyWebhook(
            "subscribe", "wrong-token", "challenge-123", CancellationToken.None);

        Assert.Equal("challenge-123", accepted.Content);
        Assert.StartsWith("text/plain", accepted.ContentType, StringComparison.Ordinal);
        Assert.IsType<ForbidResult>(rejected);
    }

    [Fact]
    public async Task InvalidSignature_IsRejectedBeforeWebhookProcessing()
    {
        var configurationReader =
            FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(Configuration());
        var controller = new FacebookMessengerLiveSupportController(configurationReader, null!);
        SetRequest(controller, "{}", "sha256=invalid");

        var response = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task ValidSignedMessage_IsPersistedOnceAcrossWebhookReplay()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var configuration = Configuration();
        var configurationReader =
            FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(configuration);
        var support = new LiveSupportService(db, new EnabledSettings());
        using var downloader = new FacebookMessengerSafeMediaDownloader(new RejectingHandler());
        var graph = new FacebookMessengerGraphClient(
            new HttpClient(new RejectingHandler()),
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
        var controller = new FacebookMessengerLiveSupportController(configurationReader, service);
        const string body = """
            {
              "object": "page",
              "entry": [{
                "id": "page-1",
                "messaging": [{
                  "sender": { "id": "psid-controller-test" },
                  "recipient": { "id": "page-1" },
                  "timestamp": 1788000000000,
                  "message": {
                    "mid": "mid.controller.durable",
                    "text": "رسالة موقعة"
                  }
                }]
              }]
            }
            """;
        SetRequest(controller, body, Signature(body));

        var firstResponse = await controller.ReceiveWebhook(CancellationToken.None);
        var persisted = await db.LiveSupportMessengerWebhookInbox
            .AsNoTracking()
            .SingleAsync();

        SetRequest(controller, body, Signature(body));
        var replayResponse = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkObjectResult>(firstResponse);
        Assert.IsType<OkObjectResult>(replayResponse);
        Assert.Equal("page-1", persisted.PageId);
        Assert.Equal("message", persisted.EventKind);
        Assert.Equal("message:mid.controller.durable", persisted.DeduplicationKey);
        Assert.Equal("Pending", persisted.Status);
        Assert.Equal(1, await db.LiveSupportMessengerWebhookInbox.CountAsync());
        Assert.Equal(
            persisted.Id,
            await db.LiveSupportMessengerWebhookInbox
                .AsNoTracking()
                .Select(inbox => inbox.Id)
                .SingleAsync());
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
            ["FacebookMessenger:Pages:0:AccessToken"] = "token-1"
        };
        return new FacebookMessengerConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    private static string Signature(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("app-secret"));
        var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return $"sha256={Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private static void SetRequest(ControllerBase controller, string body, string signature)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.Headers["X-Hub-Signature-256"] = signature;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate() { }
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

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Meta request expected.");
    }
}
