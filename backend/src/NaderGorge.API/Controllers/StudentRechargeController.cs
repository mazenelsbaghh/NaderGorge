using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Recharge;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/student/recharge")]
[Authorize]
public class StudentRechargeController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<StudentRechargeController> _logger;

    public StudentRechargeController(IMediator mediator, ILogger<StudentRechargeController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpPost("initiate")]
    public async Task<IActionResult> InitiateRecharge([FromBody] InitiateRechargeRequestDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new InitiateRechargeCommand(GetUserId(), dto.Amount, dto.TeacherId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetMyRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyRechargeRequestsQuery(GetUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id, CancelRechargeRequestDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelRechargeRequestCommand(GetUserId(), id, dto.Reason), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("submit")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitRecharge(
        [FromForm] Guid rechargeRequestId,
        [FromForm] string senderPhoneNumber,
        [FromForm] IFormFile screenshot,
        CancellationToken ct)
    {
        if (screenshot == null || screenshot.Length == 0)
        {
            return BadRequest(ApiResponse<SubmitRechargeDto>.Fail("صورة إثبات التحويل مطلوبة"));
        }

        if (screenshot.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<SubmitRechargeDto>.Fail("حجم الصورة يجب أن لا يتخطى 10 ميجا بايت"));
        }

        using var ms = new MemoryStream();
        await screenshot.CopyToAsync(ms, ct);
        var screenshotBytes = ms.ToArray();

        _logger.LogInformation(
            "Recharge proof received: FileName={FileName}, ContentType={ContentType}, Length={Length}",
            screenshot.FileName,
            screenshot.ContentType,
            screenshotBytes.Length);

        var result = await _mediator.Send(new SubmitRechargeCommand(
            rechargeRequestId,
            senderPhoneNumber,
            screenshotBytes,
            screenshot.FileName,
            screenshot.ContentType), ct);

        if (!result.Success)
        {
            _logger.LogWarning("Recharge proof rejected after validation: FileName={FileName}, ContentType={ContentType}", screenshot.FileName, screenshot.ContentType);
            return BadRequest(result);
        }

        return Ok(result);
    }
}

public record InitiateRechargeRequestDto(decimal Amount, Guid? TeacherId = null);
public record CancelRechargeRequestDto(string Reason);
