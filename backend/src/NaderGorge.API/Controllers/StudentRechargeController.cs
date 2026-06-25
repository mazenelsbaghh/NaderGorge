using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    public StudentRechargeController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId() => User.RequireUserId();

    [HttpPost("initiate")]
    public async Task<IActionResult> InitiateRecharge([FromBody] InitiateRechargeRequestDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new InitiateRechargeCommand(GetUserId(), dto.Amount), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetMyRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyRechargeRequestsQuery(GetUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
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

        var result = await _mediator.Send(new SubmitRechargeCommand(
            rechargeRequestId,
            senderPhoneNumber,
            screenshotBytes), ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record InitiateRechargeRequestDto(decimal Amount);
