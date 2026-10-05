using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/exam-room/whatsapp/webhook")]
public sealed class ExamWhatsAppWebhookController(
    IConfiguration configuration,
    ExamWhatsAppWebhookService service) : ControllerBase
{
    [HttpGet]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        return mode == "subscribe" && FixedEquals(configuration["ExamWhatsAppCloud:VerifyToken"], token) &&
            !string.IsNullOrWhiteSpace(challenge)
            ? Content(challenge, "text/plain", Encoding.UTF8)
            : Forbid();
    }

    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken ct)
    {
        if (!Configured()) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        await using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, ct);
        var body = stream.ToArray();
        if (!ValidSignature(body)) return Unauthorized();
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
            await service.RecordDeliveryEventsAsync(document.RootElement, ct);
            return Ok(new { received = true });
        }
        catch (JsonException) { return BadRequest(); }
    }

    private bool Configured() => new[] { "AppSecret", "BusinessAccountId", "PhoneNumberId" }
        .All(key => !string.IsNullOrWhiteSpace(configuration[$"ExamWhatsAppCloud:{key}"]));

    private bool ValidSignature(byte[] body)
    {
        var supplied = Request.Headers["X-Hub-Signature-256"].ToString();
        if (supplied.Length != 71 || !supplied.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        var secret = configuration["ExamWhatsAppCloud:AppSecret"]!;
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
        return FixedEquals(expected, supplied[7..].ToUpperInvariant());
    }

    private static bool FixedEquals(string? expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied)) return false;
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(supplied);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
