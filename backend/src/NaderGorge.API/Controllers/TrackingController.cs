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

    public record VideoEventRequest(Guid LessonVideoId, int WatchedSeconds, int TotalDurationSeconds = 0);

    [HttpPost("video-event")]
    public async Task<IActionResult> RecordVideoEvent([FromBody] VideoEventRequest req)
    {
        if (req.WatchedSeconds <= 0) return BadRequest("Invalid seconds");
        if (req.TotalDurationSeconds <= 0)
            return BadRequest(new { success = false, errors = new[] { "DURATION_REQUIRED" } });

        var response = await _mediator.Send(new RecordVideoEventCommand(GetUserId(), req.LessonVideoId, req.WatchedSeconds, req.TotalDurationSeconds));

        if (!response.Success)
        {
            if (response.Errors?.Contains("DURATION_REQUIRED") == true)
                return BadRequest(response);

            if (response.Errors?.Contains("WATCH_LIMIT_REACHED") == true)
                return StatusCode(403, response);

            return NotFound(response);
        }

        return Ok(response);
    }
}
