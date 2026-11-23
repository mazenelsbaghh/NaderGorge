using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/exam-room/whatsapp/statuses")]
public sealed class ExamWhatsAppStatusesController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [RequestSizeLimit(131_072)]
    public async Task<IActionResult> ReadStatuses(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var secret = configuration["ExamWhatsAppCloud:AppSecret"];
        if (string.IsNullOrWhiteSpace(secret) ||
            string.IsNullOrWhiteSpace(configuration["ExamWhatsAppCloud:BusinessAccountId"]) ||
            string.IsNullOrWhiteSpace(configuration["ExamWhatsAppCloud:PhoneNumberId"])) return StatusCode(503);
        await using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, ct);
        if (stream.Length > 131_072) return BadRequest();
        var body = stream.ToArray();
        if (!ValidSignature(body, secret)) return Unauthorized();
        StatusRequest? request;
        try { request = JsonSerializer.Deserialize<StatusRequest>(body, JsonOptions); }
        catch (JsonException) { return BadRequest(); }
        if (request?.MessageIds is not { Count: > 0 and <= 200 } ||
            request.MessageIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 512))
            return BadRequest();
        if (request.BusinessAccountId != configuration["ExamWhatsAppCloud:BusinessAccountId"] ||
            request.PhoneNumberId != configuration["ExamWhatsAppCloud:PhoneNumberId"])
            return Unauthorized();
        var events = await db.ExamWhatsAppDeliveryEvents.AsNoTracking()
            .Where(receipt => receipt.BusinessAccountId == request.BusinessAccountId &&
                receipt.PhoneNumberId == request.PhoneNumberId && request.MessageIds.Contains(receipt.MessageId))
            .OrderBy(receipt => receipt.EventUnixTime).Take(2001)
            .Select(receipt => new { receipt.MessageId, receipt.Status, receipt.EventUnixTime, receipt.ErrorCode })
            .ToListAsync(ct);
        if (events.Count > 2000) return BadRequest();
        return Ok(new { request.BusinessAccountId, request.PhoneNumberId, events });
    }

    private bool ValidSignature(byte[] body, string secret)
    {
        var timestamp = Request.Headers["X-Massar-Timestamp"].ToString();
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            Math.Abs((decimal)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > 300)
            return false;
        var supplied = Request.Headers["X-Massar-Signature"].ToString();
        if (supplied.Length != 64) return false;
        var prefix = Encoding.UTF8.GetBytes($"MASSAR-EXAM-RECEIPTS-v1\n{timestamp}\n");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed, prefix.Length);
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(supplied.ToUpperInvariant()));
    }

    public sealed record StatusRequest(string BusinessAccountId, string PhoneNumberId, List<string> MessageIds);
}
