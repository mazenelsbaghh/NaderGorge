using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Android;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/android")]
[AllowAnonymous]
public class AndroidWalletController : ControllerBase
{
    private readonly IMediator _mediator;

    public AndroidWalletController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("sync-status")]
    public async Task<IActionResult> SyncStatus([FromBody] AndroidSyncStatusRequestDto dto, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-Pairing-Token", out var tokenValues))
        {
            return BadRequest(new { success = false, message = "Pairing token is required in X-Pairing-Token header" });
        }

        var token = tokenValues.ToString();
        var result = await _mediator.Send(new AndroidSyncStatusCommand(token, dto.CurrentBalance), ct);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("sms")]
    public async Task<IActionResult> UploadSms([FromBody] AndroidSmsUploadRequestDto dto, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-Pairing-Token", out var tokenValues))
        {
            return BadRequest(new { success = false, message = "Pairing token is required in X-Pairing-Token header" });
        }

        var token = tokenValues.ToString();
        var result = await _mediator.Send(new AndroidUploadSmsCommand(
            token,
            dto.Sender,
            dto.Body,
            dto.ReceivedAt), ct);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}

public record AndroidSyncStatusRequestDto(decimal? CurrentBalance);

public record AndroidSmsUploadRequestDto(string Sender, string Body, DateTime ReceivedAt);
