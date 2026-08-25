using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support/whatsapp")]
public sealed class WhatsAppLiveSupportController(
    IConfiguration configuration,
    WhatsAppLiveSupportService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = configuration["WhatsAppCloudApi:VerifyToken"];
        return mode == "subscribe" && FixedEquals(expected, token) && !string.IsNullOrWhiteSpace(challenge)
            ? Content(challenge, "text/plain", Encoding.UTF8)
            : Forbid();
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken ct)
    {
        await using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        var bytes = body.ToArray();
        if (!ValidSignature(bytes, Request.Headers["X-Hub-Signature-256"].ToString())) return Unauthorized();
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            await service.ProcessWebhookAsync(document.RootElement, ct);
            return Ok(new { received = true });
        }
        catch (JsonException) { return BadRequest(new { received = false }); }
    }

    [Authorize(Roles = "Admin,Assistant,AssistantReviewer,Staff")]
    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<LiveSupportWhatsAppTemplateDto>>.Ok(await service.ListTemplatesAsync(ct)));

    [HasPermission("live_support.manage")]
    [HttpPost("templates/sync")]
    public async Task<IActionResult> SyncTemplates(CancellationToken ct)
    {
        try
        {
            return Ok(ApiResponse<IReadOnlyList<LiveSupportWhatsAppTemplateDto>>.Ok(
                await service.SyncTemplatesAsync(User.RequireUserId(), ct)));
        }
        catch (WhatsAppCampaignException exception)
        {
            return StatusCode(exception.StatusCode,
                ApiResponse<object>.Fail(exception.Message, [exception.Code]));
        }
    }

    private bool ValidSignature(byte[] body, string supplied)
    {
        var appSecret = configuration["WhatsAppCloudApi:AppSecret"];
        if (string.IsNullOrWhiteSpace(appSecret) || !supplied.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expected = Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
        return FixedEquals(expected, supplied[7..]);
    }

    private static bool FixedEquals(string? expected, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(supplied);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
