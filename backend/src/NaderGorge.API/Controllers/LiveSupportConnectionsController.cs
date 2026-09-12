using NaderGorge.Application.Features.LiveSupport.Interfaces;
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
[Route("api/live-support/connections")]
[HasPermission("live_support.manage")]
public sealed class LiveSupportConnectionsController(BaileysAccountService accounts, LiveSupportBlockingService blocking) : ControllerBase
{
    [HttpGet("whatsapp")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<BaileysAccountDto>>.Ok(await accounts.ListAsync(ct)));

    [HttpPost("whatsapp")]
    public Task<IActionResult> Create(CreateWhatsAppAccountRequest request, CancellationToken ct) =>
        RespondAsync(() => accounts.CreateAsync(User.RequireUserId(), request.Name, ct));

    [HttpPost("whatsapp/{id:guid}/connect")]
    public Task<IActionResult> Connect(Guid id, CancellationToken ct) => RespondAsync(() => accounts.ConnectAsync(id, ct));

    [HttpPost("whatsapp/{id:guid}/refresh")]
    public Task<IActionResult> Refresh(Guid id, CancellationToken ct) => RespondAsync(() => accounts.RefreshAsync(id, ct));

    [HttpPost("whatsapp/{id:guid}/disconnect")]
    public Task<IActionResult> Disconnect(Guid id, CancellationToken ct) => RespondAsync(() => accounts.DisconnectAsync(id, ct));

    [HttpGet("conversations/{id:guid}/block")]
    public Task<IActionResult> BlockStatus(Guid id, CancellationToken ct) => RespondAsync(() => blocking.GetAsync(id, ct));

    [HttpPut("conversations/{id:guid}/block")]
    public Task<IActionResult> Block(Guid id, SupportBlockRequest request, CancellationToken ct) =>
        RespondAsync(() => blocking.SetAsync(id, User.RequireUserId(), request, ct));

    [HttpPost("conversations/{id:guid}/block/retry")]
    public Task<IActionResult> RetryBlock(Guid id, CancellationToken ct) => RespondAsync(() => blocking.RetryAsync(id, ct));

    private async Task<IActionResult> RespondAsync<T>(Func<Task<T>> operation)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(ApiResponse<T>.Ok(await operation())); }
        catch (LiveSupportException exception)
        {
            var status = exception.Code == "NOT_FOUND" ? 404 : exception.Code == "VALIDATION_ERROR" ? 400
                : exception.Code == "VERSION_CONFLICT" ? 409 : 502;
            return StatusCode(status, ApiResponse<object>.Fail(exception.Message, [exception.Code]));
        }
    }
}

public sealed record CreateWhatsAppAccountRequest(string Name);

[ApiController]
[Route("api/live-support/baileys/webhook")]
public sealed class BaileysWebhookController(IConfiguration configuration, BaileysWebhookService webhook) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Receive(JsonElement payload, CancellationToken ct)
    {
        var expected = configuration["Baileys:ApiKey"];
        var supplied = Request.Headers["X-Baileys-Token"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied))) return Unauthorized();
        await webhook.ReceiveAsync(payload, ct);
        return Ok(new { received = true });
    }
}
