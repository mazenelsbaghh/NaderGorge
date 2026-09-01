using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support/messenger")]
public sealed class FacebookMessengerLiveSupportController(
    IFacebookMessengerRuntimeConfigurationReader configurationReader,
    FacebookMessengerLiveSupportService service) : ControllerBase
{
    private const int MaximumWebhookBytes = 1_048_576;

    [AllowAnonymous]
    [HttpGet("webhook")]
    public async Task<IActionResult> VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken ct)
    {
        FacebookMessengerRuntimeConfiguration configuration;
        try
        {
            configuration = await configurationReader.GetAsync(ct);
        }
        catch (FacebookMessengerConfigurationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        return string.Equals(mode, "subscribe", StringComparison.Ordinal) &&
               FixedEquals(configuration.VerifyToken, token) &&
               !string.IsNullOrWhiteSpace(challenge)
            ? Content(challenge, "text/plain", Encoding.UTF8)
            : Forbid();
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaximumWebhookBytes)]
    [HttpLogging(HttpLoggingFields.None)]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken ct)
    {
        if (Request.ContentLength is > MaximumWebhookBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        await using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        if (body.Length > MaximumWebhookBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        FacebookMessengerRuntimeConfiguration configuration;
        try
        {
            configuration = await configurationReader.GetAsync(ct);
        }
        catch (FacebookMessengerConfigurationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var payload = body.ToArray();
        if (!configuration.IsEnabled ||
            !ValidSignature(payload, Request.Headers["X-Hub-Signature-256"].ToString(), configuration.AppSecret))
            return Unauthorized();

        try
        {
            using var document = JsonDocument.Parse(
                payload,
                new JsonDocumentOptions { MaxDepth = 32 });
            var enqueued = await service.EnqueueWebhookAsync(document.RootElement, ct);
            return Ok(new { received = true, enqueued });
        }
        catch (JsonException)
        {
            return BadRequest(new { received = false });
        }
        catch (FacebookMessengerWebhookException exception) when (!exception.IsRetryable)
        {
            return Conflict(new { received = false, code = exception.ErrorCode });
        }
    }

    private static bool ValidSignature(byte[] body, string supplied, string appSecret)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrWhiteSpace(appSecret) ||
            !supplied.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        byte[] suppliedDigest;
        try
        {
            suppliedDigest = Convert.FromHexString(supplied[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expectedDigest = hmac.ComputeHash(body);
        return suppliedDigest.Length == expectedDigest.Length &&
               CryptographicOperations.FixedTimeEquals(expectedDigest, suppliedDigest);
    }

    private static bool FixedEquals(string? expected, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
