using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Features.Codes.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("codes")]
public class CodesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CodesController(IMediator mediator) => _mediator = mediator;

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateCodeRequest body)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new ActivateCodeCommand(userId, body.Code);
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record ActivateCodeRequest(string Code);
