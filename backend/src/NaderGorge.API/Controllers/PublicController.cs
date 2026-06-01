using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Public.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlatformStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlatformStatsQuery(), ct);
        return Ok(result);
    }
}
