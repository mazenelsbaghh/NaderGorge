using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.API.Filters;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("codes")]
public class CodesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CodesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("recharge-recommendations/{redemptionId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> RechargeRecommendations(Guid redemptionId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var response = await _mediator.Send(new NaderGorge.Application.Features.Codes.Queries.GetRechargeRecommendationsQuery(userId, redemptionId, page), ct);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPost("activate")]
    [Idempotent]
    public async Task<IActionResult> Activate([FromBody] ActivateCodeRequest body)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new ActivateCodeCommand(userId, body.Code);
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("validate/{code}")]
    public async Task<IActionResult> Validate(string code)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _mediator.Send(new NaderGorge.Application.Features.Codes.Queries.ValidateCodeQuery(code, userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record ActivateCodeRequest(string Code);
