using NaderGorge.Application.Features.LiveSupport.Interfaces;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Services;

namespace NaderGorge.Infrastructure.Services;

// The private Node bridge owns Baileys sockets and encrypted authentication state.
public sealed class BaileysWhatsAppClient(HttpClient http, IConfiguration configuration)
{
    public bool IsConfigured => Uri.TryCreate(configuration["Baileys:BaseUrl"], UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(configuration["Baileys:ApiKey"]);

    public Task<JsonElement> CreateAsync(string instance, CancellationToken ct)
    {
        if (!IsConfigured) throw Failure("BAILEYS_NOT_CONFIGURED");
        return RequestAsync(HttpMethod.Post, "sessions", new { sessionId = instance }, ct);
    }

    public Task<JsonElement> ConnectAsync(string instance, CancellationToken ct) =>
        RequestAsync(HttpMethod.Post, $"sessions/{Uri.EscapeDataString(instance)}/connect", null, ct);

    public Task<JsonElement> StateAsync(string instance, CancellationToken ct) =>
        RequestAsync(HttpMethod.Get, $"sessions/{Uri.EscapeDataString(instance)}", null, ct);

    public Task<JsonElement> LogoutAsync(string instance, CancellationToken ct) =>
        RequestAsync(HttpMethod.Delete, $"sessions/{Uri.EscapeDataString(instance)}", null, ct);

    public async Task SetBlockedAsync(string instance, string phone, bool blocked, CancellationToken ct) =>
        _ = await RequestAsync(HttpMethod.Post, $"sessions/{Uri.EscapeDataString(instance)}/block",
            new { number = phone, status = blocked ? "block" : "unblock" }, ct);

    public async Task<WhatsAppCloudService.SendTestMessageResult> SendTextAsync(
        string instance, string phone, string content, CancellationToken ct)
    {
        var response = await RequestAsync(HttpMethod.Post, $"sessions/{Uri.EscapeDataString(instance)}/text",
            new { number = phone, text = content }, ct);
        return Accepted(response, instance, phone);
    }

    public async Task<WhatsAppCloudService.SendTestMessageResult> SendMediaAsync(
        string instance, WhatsAppCloudService.MediaMessageRequest media, CancellationToken ct)
    {
        var audio = media.MediaType == "audio";
        var body = audio
            ? (object)new { number = media.RecipientPhoneNumber, mimetype = media.ContentType, audio = Convert.ToBase64String(media.Content) }
            : new { number = media.RecipientPhoneNumber, mediatype = media.MediaType,
                mimetype = media.ContentType, fileName = media.FileName, caption = media.Caption,
                media = Convert.ToBase64String(media.Content) };
        var response = await RequestAsync(HttpMethod.Post,
            $"sessions/{Uri.EscapeDataString(instance)}/{(audio ? "audio" : "media")}", body, ct);
        return Accepted(response, instance, media.RecipientPhoneNumber);
    }

    public async Task<WhatsAppCloudService.DownloadedMedia> DownloadMediaAsync(
        string instance, JsonElement message, CancellationToken ct)
    {
        var response = await RequestAsync(HttpMethod.Post,
            $"sessions/{Uri.EscapeDataString(instance)}/download", new { message }, ct);
        var base64 = Text(response, "base64");
        if (string.IsNullOrWhiteSpace(base64) || base64.Length > 14_000_000)
            throw Failure("BAILEYS_MEDIA_INVALID");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { throw Failure("BAILEYS_MEDIA_INVALID"); }
        return new(bytes, Text(response, "mimetype") ?? "application/octet-stream",
            Path.GetFileName(Text(response, "fileName") ?? "whatsapp-attachment"));
    }

    public static string MessageId(string instance, string providerId) => $"baileys:{instance}:{providerId}";

    private static WhatsAppCloudService.SendTestMessageResult Accepted(JsonElement response, string instance, string phone)
    {
        var id = response.TryGetProperty("key", out var key) ? Text(key, "id") : null;
        return string.IsNullOrWhiteSpace(id)
            ? new(false, "تعذر التأكد من إرسال رسالة واتساب.", phone, null, 502, "BAILEYS_DELIVERY_UNCERTAIN")
            : new(true, "تم إرسال الرسالة.", phone, MessageId(instance, id), 200, null);
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (!IsConfigured) throw Failure("BAILEYS_NOT_CONFIGURED");
        using var request = new HttpRequestMessage(method, configuration["Baileys:BaseUrl"]!.TrimEnd('/') + "/" + path);
        request.Headers.Add("X-Baileys-Token", configuration["Baileys:ApiKey"]);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw Failure(response.StatusCode == HttpStatusCode.NotFound ? "BAILEYS_INSTANCE_NOT_FOUND" : "BAILEYS_REQUEST_FAILED");
            await response.Content.LoadIntoBufferAsync(16 * 1024 * 1024, timeout.Token);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            return document.RootElement.Clone();
        }
        catch (HttpRequestException) { throw Failure("BAILEYS_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Failure("BAILEYS_REQUEST_UNCERTAIN"); }
        catch (JsonException) { throw Failure("BAILEYS_INVALID_RESPONSE"); }
    }

    internal static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static LiveSupportException Failure(string code) => new(code,
        code == "BAILEYS_NOT_CONFIGURED" ? "اتصال واتساب QR غير مهيأ على الخادم." : "تعذر إتمام طلب واتساب. راجع حالة الاتصال ثم حاول مجددًا.");
}
