using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Tracking.Commands;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrackingController : ControllerBase
{
    private readonly IMediator _mediator;

    public TrackingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    public record VideoEventRequest(Guid LessonVideoId, int WatchedSeconds);

    [HttpPost("video-event")]
    public async Task<IActionResult> RecordVideoEvent([FromBody] VideoEventRequest req)
    {
        if (req.WatchedSeconds <= 0) return BadRequest("Invalid seconds");

        var response = await _mediator.Send(new RecordVideoEventCommand(GetUserId(), req.LessonVideoId, req.WatchedSeconds));

        if (!response.Success)
        {
            if (response.Errors!.Contains("Maximum watch limit reached"))
                return StatusCode(403, response);
                
            return NotFound(response);
        }

        return Ok(response);
    }
}
