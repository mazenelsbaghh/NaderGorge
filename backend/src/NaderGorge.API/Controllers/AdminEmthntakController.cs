using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/emthntak")]
[Authorize]
public sealed class AdminEmthntakController(IConfiguration configuration, IHttpClientFactory clients) : ControllerBase
{
    private static readonly string[] Capabilities = ["content", "review", "publish", "support", "finance", "settings"];
    public sealed record BridgeRequest(string Path, string Method, JsonElement? Body);

    private string[] AllowedCapabilities() => Capabilities.Where(capability =>
        User.IsInRole("Admin") || User.HasClaim("permission", "emthntak." + capability)).ToArray();

    [HttpGet("configuration")]
    public IActionResult Configuration()
    {
        var permissions = AllowedCapabilities();
        if (permissions.Length == 0) return Forbid();
        var frameUrl = configuration["EmthntakAdmin:FrameUrl"];
        if (!SafeAddress(frameUrl)) return StatusCode(503, new { error = "ربط امتحاناتك لم يُعد بعد." });
        return Ok(new { frameUrl, permissions });
    }

    [HttpPost("request")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> ForwardRequest(BridgeRequest request, CancellationToken ct)
    {
        var permissions = AllowedCapabilities();
        if (permissions.Length == 0) return Forbid();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        var apiUrl = configuration["EmthntakAdmin:ApiUrl"];
        var keyFile = configuration["EmthntakAdmin:KeyFile"];
        if (!SafeAddress(apiUrl) || string.IsNullOrWhiteSpace(keyFile))
            return StatusCode(503, new { error = "ربط امتحاناتك لم يُعد بعد." });
        string key;
        try { key = (await System.IO.File.ReadAllTextAsync(keyFile, ct)).Trim(); }
        catch (IOException) { return StatusCode(503, new { error = "مفتاح ربط امتحاناتك غير متاح." }); }
        if (Encoding.UTF8.GetByteCount(key) < 32) return StatusCode(503, new { error = "إعداد الربط غير مكتمل." });
        var envelope = JsonSerializer.Serialize(new {
            aud = "emthntak-admin", subject = actor.ToString(), name = User.Identity?.Name ?? "موظف مسار",
            permissions, expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30,
            nonce = Guid.NewGuid().ToString(), path = request.Path, method = request.Method, body = request.Body
        });
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(envelope))).ToLowerInvariant();
        using var message = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        message.Headers.Host = configuration["EmthntakAdmin:ApiHost"] ?? "admin.localhost:8080";
        message.Headers.TryAddWithoutValidation("Origin", new Uri(apiUrl!).Scheme + "://" + message.Headers.Host);
        message.Headers.Add("X-Massar-Signature", signature);
        message.Content = JsonContent.Create(new { envelope });
        try {
            using var response = await clients.CreateClient("EmthntakAdmin").SendAsync(message, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = content };
        }
        catch (HttpRequestException) { return StatusCode(503, new { error = "خدمة امتحاناتك غير متاحة حاليًا." }); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(504, new { error = "انتهت مهلة الاتصال بامتحاناتك." }); }
    }

    private static bool SafeAddress(string? address) => Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Scheme == "https" || uri.Scheme == "http" && (uri.IsLoopback || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)));
}
