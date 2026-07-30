using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Gamification.Queries;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireStudent")]
public class GamificationController : ControllerBase
{
    private readonly IMediator _mediator;

    public GamificationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var studentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdClaim, out var studentId))
            return Unauthorized(ApiResponse<Guid>.Fail("Invalid user token."));

        var query = new GetGamificationStatusQuery(studentId);
        var result = await _mediator.Send(query, ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
