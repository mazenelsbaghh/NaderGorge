using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services;

public sealed class ExamWhatsAppWebhookService(AppDbContext db, IConfiguration configuration)
{
    public async Task RecordDeliveryEventsAsync(JsonElement webhook, CancellationToken ct)
    {
        var receipts = DeliveryEvents(webhook);
        foreach (var receipt in receipts)
            await StoreReceiptAsync(receipt, ct);
    }

    private List<ExamWhatsAppDeliveryEvent> DeliveryEvents(JsonElement webhook)
    {
        if (Text(webhook, "object") != "whatsapp_business_account")
            throw new JsonException("Unsupported webhook object.");
        var receipts = new List<ExamWhatsAppDeliveryEvent>();
        foreach (var entry in Array(webhook, "entry"))
        {
            if (Text(entry, "id") != configuration["ExamWhatsAppCloud:BusinessAccountId"]) continue;
            foreach (var change in Array(entry, "changes"))
                receipts.AddRange(ChangeReceipts(change));
        }
        return receipts;
    }

    private IEnumerable<ExamWhatsAppDeliveryEvent> ChangeReceipts(JsonElement change)
    {
        if (Text(change, "field") != "messages") yield break;
        var payload = Object(change, "value");
        var phoneId = Text(Object(payload, "metadata"), "phone_number_id");
        if (phoneId != configuration["ExamWhatsAppCloud:PhoneNumberId"]) yield break;
        if (!payload.TryGetProperty("statuses", out var statuses)) yield break;
        if (statuses.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid statuses.");
        foreach (var status in statuses.EnumerateArray())
            yield return Receipt(status, phoneId);
    }

    private ExamWhatsAppDeliveryEvent Receipt(JsonElement status, string phoneId)
    {
        var receipt = new ExamWhatsAppDeliveryEvent
        {
            BusinessAccountId = configuration["ExamWhatsAppCloud:BusinessAccountId"]!,
            PhoneNumberId = phoneId,
            MessageId = Text(status, "id"),
            Status = Text(status, "status"),
            EventUnixTime = Timestamp(status),
            ErrorCode = ErrorCode(status)
        };
        if (receipt.MessageId.Length is < 1 or > 512 ||
            receipt.Status is not ("sent" or "delivered" or "read" or "failed" or "deleted"))
            throw new JsonException("Invalid delivery receipt.");
        receipt.Fingerprint = Fingerprint(receipt);
        return receipt;
    }

    private async Task StoreReceiptAsync(ExamWhatsAppDeliveryEvent receipt, CancellationToken ct)
    {
        // Meta retries can reach different nodes; the database arbitrates duplicates.
        var receivedAt = DateTime.SpecifyKind(receipt.ReceivedAt, DateTimeKind.Unspecified);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ExamWhatsAppDeliveryEvents"
            ("Fingerprint", "BusinessAccountId", "PhoneNumberId", "MessageId", "Status", "EventUnixTime", "ErrorCode", "ReceivedAt")
            VALUES ({receipt.Fingerprint}, {receipt.BusinessAccountId}, {receipt.PhoneNumberId},
                {receipt.MessageId}, {receipt.Status}, {receipt.EventUnixTime}, {receipt.ErrorCode}, {receivedAt})
            ON CONFLICT ("Fingerprint") DO NOTHING
            """, ct);
    }

    private static string Fingerprint(ExamWhatsAppDeliveryEvent receipt)
    {
        var identity = JsonSerializer.Serialize(new object?[]
        {
            receipt.BusinessAccountId, receipt.PhoneNumberId, receipt.MessageId,
            receipt.Status, receipt.EventUnixTime, receipt.ErrorCode
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static long Timestamp(JsonElement status)
    {
        if (!long.TryParse(Text(status, "timestamp"), NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) ||
            timestamp is < 0 or > 253402300799)
            throw new JsonException("Invalid delivery timestamp.");
        return timestamp;
    }

    private static int? ErrorCode(JsonElement status)
    {
        if (!status.TryGetProperty("errors", out var errors)) return null;
        if (errors.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid delivery errors.");
        foreach (var error in errors.EnumerateArray())
        {
            if (error.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid delivery error.");
            if (error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number &&
                code.TryGetInt32(out var number)) return number;
        }
        return null;
    }

    private static string Text(JsonElement parent, string property)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(property, out var child) ||
            child.ValueKind != JsonValueKind.String)
            throw new JsonException("Invalid webhook field.");
        return child.GetString()!;
    }

    private static JsonElement Object(JsonElement parent, string property)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(property, out var child) ||
            child.ValueKind != JsonValueKind.Object)
            throw new JsonException("Invalid webhook object.");
        return child;
    }

    private static JsonElement.ArrayEnumerator Array(JsonElement parent, string property)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(property, out var child) ||
            child.ValueKind != JsonValueKind.Array)
            throw new JsonException("Invalid webhook array.");
        return child.EnumerateArray();
    }
}
