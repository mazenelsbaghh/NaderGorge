using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NaderGorge.Infrastructure.Services;

public sealed record FacebookMessengerWebhookEvent(
    string PageId,
    string EventKind,
    string DeduplicationKey,
    string PayloadHash,
    string PayloadJson);

public sealed class FacebookMessengerWebhookParser
{
    private readonly IReadOnlySet<string>? _fallbackPageIds;

    public FacebookMessengerWebhookParser()
    {
    }

    public FacebookMessengerWebhookParser(FacebookMessengerConfiguration configuration)
    {
        _fallbackPageIds = configuration.Pages
            .Select(page => page.PageId)
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<FacebookMessengerWebhookEvent> Parse(JsonElement webhook)
    {
        if (_fallbackPageIds is null)
            throw new InvalidOperationException("Configured Page IDs are required to parse a Messenger webhook.");
        return Parse(webhook, _fallbackPageIds);
    }

    public IReadOnlyList<FacebookMessengerWebhookEvent> Parse(
        JsonElement webhook,
        IReadOnlySet<string> configuredPageIds)
    {
        if (Text(webhook, "object") != "page") return [];
        var events = new List<FacebookMessengerWebhookEvent>();
        foreach (var entry in Array(webhook, "entry"))
        {
            var pageId = Text(entry, "id");
            if (pageId is null || !configuredPageIds.Contains(pageId)) continue;
            foreach (var messagingEvent in Array(entry, "messaging"))
            {
                if (!MatchesPage(messagingEvent, pageId)) continue;
                if (ParsedEvent(pageId, messagingEvent) is { } parsedEvent)
                    events.Add(parsedEvent);
            }
        }
        return events;
    }

    private static FacebookMessengerWebhookEvent? ParsedEvent(
        string pageId,
        JsonElement messagingEvent)
    {
        var eventKind = EventKind(messagingEvent);
        if (eventKind is null) return null;
        var deduplicationKey = DeduplicationKey(eventKind, messagingEvent);
        if (deduplicationKey is null) return null;
        var payloadJson = messagingEvent.GetRawText();
        var payloadHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant();
        return new FacebookMessengerWebhookEvent(
            pageId,
            eventKind,
            deduplicationKey,
            payloadHash,
            payloadJson);
    }

    private static string? EventKind(JsonElement messagingEvent)
    {
        if (messagingEvent.TryGetProperty("message", out var message))
            return message.TryGetProperty("is_echo", out var echo) && echo.ValueKind == JsonValueKind.True
                ? "message_echo"
                : "message";
        if (messagingEvent.TryGetProperty("delivery", out _)) return "delivery";
        if (messagingEvent.TryGetProperty("read", out _)) return "read";
        if (messagingEvent.TryGetProperty("postback", out _)) return "postback";
        return null;
    }

    private static string? DeduplicationKey(string eventKind, JsonElement messagingEvent)
    {
        var senderPsid = CustomerPsid(eventKind, messagingEvent);
        if (senderPsid is null) return null;
        return eventKind switch
        {
            "message" => NestedText(messagingEvent, "message", "mid") is { } mid
                ? $"message:{mid}"
                : null,
            "message_echo" => NestedText(messagingEvent, "message", "mid") is { } echoMid
                ? $"message_echo:{echoMid}"
                : null,
            "delivery" => DeliveryKey(senderPsid, messagingEvent),
            "read" => WatermarkKey("read", senderPsid, messagingEvent, "read"),
            "postback" => PostbackKey(senderPsid, messagingEvent),
            _ => null
        };
    }

    private static string? WatermarkKey(
        string prefix,
        string senderPsid,
        JsonElement messagingEvent,
        string property)
    {
        if (!messagingEvent.TryGetProperty(property, out var receipt) ||
            !receipt.TryGetProperty("watermark", out var watermark) ||
            !watermark.TryGetInt64(out var milliseconds)) return null;
        return $"{prefix}:{senderPsid}:{milliseconds}";
    }

    private static string? DeliveryKey(string senderPsid, JsonElement messagingEvent)
    {
        var watermarkKey = WatermarkKey("delivery", senderPsid, messagingEvent, "delivery");
        if (watermarkKey is null || !messagingEvent.TryGetProperty("delivery", out var delivery))
            return watermarkKey;
        var messageIds = Array(delivery, "mids")
            .Where(mid => mid.ValueKind == JsonValueKind.String)
            .Select(mid => mid.GetString()!)
            .OrderBy(mid => mid, StringComparer.Ordinal)
            .ToArray();
        if (messageIds.Length == 0) return watermarkKey;
        var midsHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', messageIds)))).ToLowerInvariant()[..16];
        return $"{watermarkKey}:{midsHash}";
    }

    private static string? PostbackKey(string senderPsid, JsonElement messagingEvent)
    {
        if (!messagingEvent.TryGetProperty("postback", out var postback)) return null;
        var mid = Text(postback, "mid");
        if (mid is not null) return $"postback:{mid}";
        var timestamp = Number(messagingEvent, "timestamp");
        var payload = Text(postback, "payload");
        if (!timestamp.HasValue || payload is null) return null;
        var payloadHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()[..16];
        return $"postback:{senderPsid}:{timestamp}:{payloadHash}";
    }

    private static string? CustomerPsid(string eventKind, JsonElement messagingEvent) =>
        eventKind == "message_echo"
            ? NestedText(messagingEvent, "recipient", "id")
            : NestedText(messagingEvent, "sender", "id");

    private static bool MatchesPage(JsonElement messagingEvent, string pageId)
    {
        var messageIsEcho = messagingEvent.TryGetProperty("message", out var message) &&
            message.TryGetProperty("is_echo", out var echo) &&
            echo.ValueKind == JsonValueKind.True;
        var pageParticipant = messageIsEcho
            ? NestedText(messagingEvent, "sender", "id")
            : NestedText(messagingEvent, "recipient", "id");
        return string.Equals(pageParticipant, pageId, StringComparison.Ordinal);
    }

    internal static IReadOnlyList<JsonElement> Array(JsonElement element, string property) =>
        element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(entry => entry.Clone()).ToArray()
            : [];

    internal static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var text) &&
        text.ValueKind == JsonValueKind.String
            ? text.GetString()
            : null;

    internal static string? NestedText(
        JsonElement element,
        string container,
        string property) =>
        element.TryGetProperty(container, out var nested)
            ? Text(nested, property)
            : null;

    internal static long? Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var number) && number.TryGetInt64(out var parsed)
            ? parsed
            : null;
}
