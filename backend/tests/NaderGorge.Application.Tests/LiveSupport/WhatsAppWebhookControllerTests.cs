using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class WhatsAppWebhookControllerTests
{
    [Fact]
    public void VerifyWebhook_ReturnsChallengeOnlyForExactConfiguredToken()
    {
        var controller = new WhatsAppLiveSupportController(Configuration(), null!);

        var accepted = Assert.IsType<ContentResult>(
            controller.VerifyWebhook("subscribe", "verify-token", "challenge-123"));
        var rejected = controller.VerifyWebhook("subscribe", "wrong-token", "challenge-123");

        Assert.Equal("challenge-123", accepted.Content);
        Assert.StartsWith("text/plain", accepted.ContentType, StringComparison.Ordinal);
        Assert.IsType<ForbidResult>(rejected);
    }

    [Fact]
    public async Task ReceiveWebhook_RejectsBodyWithInvalidSignature()
    {
        var controller = new WhatsAppLiveSupportController(Configuration(), null!);
        SetRequest(controller, "{}", "sha256=invalid");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ReceiveWebhook_AcceptsBodyWithValidSignature()
    {
        await using var db = TestAppDbContextFactory.Create();
        var configuration = Configuration();
        var support = new LiveSupportService(db, new LiveSupportEnabledSettings());
        var cloud = new WhatsAppCloudService(
            new HttpClient(), configuration, NullLogger<WhatsAppCloudService>.Instance);
        var service = new WhatsAppLiveSupportService(
            db, support, new RejectingStorage(), cloud, new LiveSupportEventWriter(db), configuration,
            new StubWhatsAppCampaignService());
        var controller = new WhatsAppLiveSupportController(configuration, service);
        const string body = "{\"object\":\"unsupported\"}";
        SetRequest(controller, body, Signature(body));

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ReceiveWebhook_RejectedInboundMediaIsAcknowledgedAndRecordedWithoutAttachment()
    {
        // Regression: safety-rejected WhatsApp media previously escaped as HTTP 500 and triggered retries.
        await using var db = TestAppDbContextFactory.Create();
        var configuration = Configuration();
        var support = new LiveSupportService(db, new LiveSupportEnabledSettings());
        var cloud = new WhatsAppCloudService(
            new HttpClient(new InboundMediaHandler()),
            configuration,
            NullLogger<WhatsAppCloudService>.Instance);
        var service = new WhatsAppLiveSupportService(
            db,
            support,
            new InvalidContentStorage(),
            cloud,
            new LiveSupportEventWriter(db),
            configuration,
            new StubWhatsAppCampaignService());
        var controller = new WhatsAppLiveSupportController(configuration, service);
        SetRequest(controller, MediaWebhookBody, Signature(MediaWebhookBody));

        var response = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        var message = Assert.Single(db.LiveSupportMessages);
        Assert.Equal(LiveSupportMessageType.Text, message.Type);
        Assert.Contains("غير متاح أو غير مدعوم", message.Content);
        Assert.Null(message.AttachmentId);
        Assert.Single(db.LiveSupportWhatsAppMessages);
        Assert.Empty(db.LiveSupportAttachments);
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsAppCloudApi:VerifyToken"] = "verify-token",
            ["WhatsAppCloudApi:AppSecret"] = "app-secret",
            ["WhatsAppCloudApi:AccessToken"] = "access-token",
            ["WhatsAppCloudApi:BusinessAccountId"] = "business-id",
            ["WhatsAppCloudApi:PhoneNumberId"] = "phone-id"
        }).Build();

    private static string Signature(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("app-secret"));
        return $"sha256={Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant()}";
    }

    private static void SetRequest(ControllerBase controller, string body, string signature)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.Headers["X-Hub-Signature-256"] = signature;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }

    private sealed class RejectingStorage : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(
            Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct) =>
            throw new InvalidOperationException("No media expected.");

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No media expected.");

        public Task DeleteAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No media expected.");
    }

    private sealed class InvalidContentStorage : ILiveSupportAttachmentStorage
    {
        public Task<LiveSupportStoredAttachment> SaveAsync(
            Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct) =>
            throw new InvalidUploadContentException("Uploaded file content type does not match its bytes.");

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No media read expected.");

        public Task DeleteAsync(string storagePath, CancellationToken ct) =>
            throw new InvalidOperationException("No media deletion expected.");
    }

    private sealed class InboundMediaHandler : HttpMessageHandler
    {
        private int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requestCount++;
            return Task.FromResult(_requestCount == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"url\":\"https://media.example/rejected\",\"mime_type\":\"image/jpeg\"}",
                        Encoding.UTF8,
                        "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47])
                });
        }
    }

    private const string MediaWebhookBody = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "business-id",
            "changes": [{
              "value": {
                "metadata": { "phone_number_id": "phone-id" },
                "contacts": [{ "wa_id": "201099999999", "profile": { "name": "عميل واتساب" } }],
                "messages": [{
                  "id": "wamid.rejected-media",
                  "from": "201099999999",
                  "timestamp": "1787529600",
                  "type": "image",
                  "image": { "id": "rejected-media", "caption": "صورة" }
                }]
              }
            }]
          }]
        }
        """;
}
