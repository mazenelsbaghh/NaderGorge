using NaderGorge.Application.Features.LiveSupport.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BaileysWebhookService(IAppDbContext db, WhatsAppLiveSupportService support)
{
    public async Task ReceiveAsync(JsonElement payload, CancellationToken ct)
    {
        var instance = BaileysWhatsAppClient.Text(payload, "sessionId");
        var account = await db.LiveSupportWhatsAppAccounts.SingleOrDefaultAsync(item => item.InstanceName == instance, ct);
        if (account is null || !payload.TryGetProperty("data", out var data)) return;
        var eventName = BaileysWhatsAppClient.Text(payload, "event");
        if (eventName == "connection")
        {
            var state = BaileysWhatsAppClient.Text(data, "state");
            account.Status = state == "open" ? "Connected" : state == "connecting" ? "Connecting" : "Disconnected";
            var ownerJid = BaileysWhatsAppClient.Text(data, "wuid");
            if (ownerJid?.EndsWith("@s.whatsapp.net", StringComparison.Ordinal) == true)
            {
                var phone = ownerJid.Split('@')[0].Split(':')[0];
                if (account.PhoneNumber is not null && account.PhoneNumber != phone) return;
                account.PhoneNumber = phone;
            }
            account.Version++;
            account.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }
        if (eventName == "receipt")
        {
            var statusCode = data.TryGetProperty("status", out var code) && code.TryGetInt32(out var status) ? status : -1;
            var receiptStatus = statusCode switch { 0 => "failed", 2 => "sent", 3 => "delivered", 4 or 5 => "read", _ => null };
            var providerId = BaileysWhatsAppClient.Text(data, "id");
            if (receiptStatus is not null && providerId is not null)
                await support.ApplyStatusAsync(JsonSerializer.SerializeToElement(new
                {
                    id = BaileysWhatsAppClient.MessageId(account.InstanceName, providerId), status = receiptStatus,
                    timestamp = data.TryGetProperty("timestamp", out var stamp) ? stamp.ToString() : "0"
                }), ct);
            return;
        }
        if (!account.IsEnabled || eventName != "message") return;
        var messages = data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().ToArray() : [data];
        foreach (var message in messages)
        {
            var normalized = NormalizeMessage(account.InstanceName, message);
            if (normalized is null) continue;
            var contact = JsonSerializer.SerializeToElement(new
            {
                contacts = new[] { new { profile = new { name = BaileysWhatsAppClient.Text(message, "pushName") } } }
            });
            await support.IngestAsync(contact, normalized.Value, ct, account, message);
        }
    }

    internal static JsonElement? NormalizeMessage(string instance, JsonElement envelope)
    {
        if (!envelope.TryGetProperty("key", out var key) ||
            key.TryGetProperty("fromMe", out var fromMe) && fromMe.ValueKind == JsonValueKind.True ||
            !envelope.TryGetProperty("message", out var message)) return null;
        var jid = BaileysWhatsAppClient.Text(key, "remoteJid");
        if (jid?.EndsWith("@g.us", StringComparison.Ordinal) == true || jid == "status@broadcast") return null;
        if (jid?.EndsWith("@lid", StringComparison.Ordinal) == true)
            jid = BaileysWhatsAppClient.Text(key, "remoteJidAlt");
        if (jid?.EndsWith("@s.whatsapp.net", StringComparison.Ordinal) != true)
            throw new LiveSupportException("BAILEYS_PHONE_UNRESOLVED", "تعذر تحديد رقم مرسل واتساب.");
        var id = BaileysWhatsAppClient.Text(key, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (message.TryGetProperty("reactionMessage", out _) || message.TryGetProperty("protocolMessage", out _)) return null;
        var text = BaileysWhatsAppClient.Text(message, "conversation");
        if (text is null && message.TryGetProperty("extendedTextMessage", out var extended))
            text = BaileysWhatsAppClient.Text(extended, "text");
        var type = "text";
        JsonElement media = default;
        foreach (var candidate in new[] { "image", "audio", "video", "document" })
            if (message.TryGetProperty(candidate + "Message", out media)) { type = candidate; break; }
        var timestamp = envelope.TryGetProperty("messageTimestamp", out var stamp)
            ? stamp.ToString() : DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["id"] = BaileysWhatsAppClient.MessageId(instance, id),
            ["from"] = jid.Split('@')[0], ["timestamp"] = timestamp, ["type"] = type,
            [type] = type == "text" ? new { body = text ?? "رسالة واتساب غير نصية" }
                : new { id, caption = BaileysWhatsAppClient.Text(media, "caption") }
        });
    }
}
