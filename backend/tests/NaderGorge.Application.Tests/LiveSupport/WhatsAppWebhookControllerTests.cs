using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Services;
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

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsAppCloudApi:VerifyToken"] = "verify-token",
            ["WhatsAppCloudApi:AppSecret"] = "app-secret",
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
}
