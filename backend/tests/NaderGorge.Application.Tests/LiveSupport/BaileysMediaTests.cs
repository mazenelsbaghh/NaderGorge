using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class BaileysMediaTests
{
    [Fact]
    public async Task Unsupported_image_body_is_a_permanent_media_failure()
    {
        using var http = new HttpClient(new ImageResponseHandler("<html>not an image</html>"u8.ToArray(), "image/jpeg"));
        var exception = await Assert.ThrowsAsync<LiveSupportException>(() => Client(http).DownloadMediaAsync("account-a",
            JsonSerializer.SerializeToElement(new { message = new { imageMessage = new { } } }), CancellationToken.None));
        Assert.Equal("BAILEYS_MEDIA_UNSUPPORTED", exception.Code);
    }

    [Theory]
    [InlineData("application/octet-stream")]
    [InlineData("image/png")]
    [InlineData("image/jpg")]
    public async Task Qr_image_with_missing_or_stale_mime_is_validated_using_its_actual_bytes(string declaredType)
    {
        using var image = new Image<Rgba32>(2, 2);
        using var encoded = new MemoryStream();
        await image.SaveAsJpegAsync(encoded);
        using var http = new HttpClient(new ImageResponseHandler(encoded.ToArray(), declaredType));
        var downloaded = await Client(http).DownloadMediaAsync("account-a",
            JsonSerializer.SerializeToElement(new { message = new { imageMessage = new { mimetype = declaredType } } }), CancellationToken.None);
        var root = Path.Combine(Path.GetTempPath(), $"qr-image-{Guid.NewGuid():N}");
        try
        {
            var storage = new LiveSupportAttachmentStorage(new SharedFileStorage(
                new Dictionary<SharedFileArea, string> { [SharedFileArea.LiveSupport] = root }));
            using var input = new MemoryStream(downloaded.Content);
            var saved = await storage.SaveAsync(input, downloaded.FileName, downloaded.ContentType, input.Length, CancellationToken.None);
            Assert.Equal("image/webp", saved.ContentType);
            await using var stored = await storage.OpenReadAsync(saved.StoragePath, CancellationToken.None);
            using var decoded = await Image.LoadAsync(stored);
            Assert.Equal(2, decoded.Width);
            Assert.Equal(2, decoded.Height);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Pdf_larger_than_ten_megabytes_round_trips_through_the_bridge_contract()
    {
        var bytes = new byte[11 * 1024 * 1024];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        using var handler = new BridgeMediaHandler(bytes);
        using var http = new HttpClient(handler);
        var client = Client(http);

        var sent = await client.SendMediaAsync("account-a", new WhatsAppCloudService.MediaMessageRequest(
            "201099999999", "document", "lesson.pdf", "application/pdf", bytes, "lesson"), CancellationToken.None);
        Assert.True(sent.Success);
        Assert.Equal("baileys:account-a:pdf-1", sent.MetaMessageId);
        var downloaded = await client.DownloadMediaAsync("account-a", JsonSerializer.SerializeToElement(new { key = new { id = "pdf-1" } }), CancellationToken.None);
        Assert.Equal(bytes, downloaded.Content);
        Assert.Equal("application/pdf", downloaded.ContentType);
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, "BAILEYS_MEDIA_UNAVAILABLE")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "BAILEYS_MEDIA_TOO_LARGE")]
    public async Task Permanent_media_failure_is_distinguished_from_retryable_bridge_failure(HttpStatusCode status, string expectedCode)
    {
        using var http = new HttpClient(new UnavailableMediaHandler(status));
        var error = await Assert.ThrowsAsync<LiveSupportException>(() => Client(http).DownloadMediaAsync(
            "account-a", JsonSerializer.SerializeToElement(new { }), CancellationToken.None));
        Assert.Equal(expectedCode, error.Code);
    }

    private static BaileysWhatsAppClient Client(HttpClient http) => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Baileys:BaseUrl"] = "http://bridge.test",
            ["Baileys:ApiKey"] = "test-key"
        }).Build());

    private sealed class BridgeMediaHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/media", StringComparison.Ordinal))
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal("document", body.RootElement.GetProperty("mediatype").GetString());
                Assert.Equal("lesson.pdf", body.RootElement.GetProperty("fileName").GetString());
                Assert.Equal(bytes, Convert.FromBase64String(body.RootElement.GetProperty("media").GetString()!));
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { key = new { id = "pdf-1" } }) };
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
                { base64 = Convert.ToBase64String(bytes), mimetype = "application/pdf", fileName = "lesson.pdf" }) };
        }
    }

    private sealed class UnavailableMediaHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class ImageResponseHandler(byte[] bytes, string declaredType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
                { base64 = Convert.ToBase64String(bytes), mimetype = declaredType, fileName = "whatsapp-image.jpg" }) });
    }
}
