using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize]
public class HrController : ControllerBase
{
    private readonly IMediator _mediator;

    public HrController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("attendance/clock-in")]
    public async Task<IActionResult> ClockIn()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "Unknown";

        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";

        var command = new ClockInCommand(userId, ipAddress, userAgent);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpPost("attendance/clock-out")]
    public async Task<IActionResult> ClockOut()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var command = new ClockOutCommand(userId);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpGet("attendance/my")]
    public async Task<ActionResult<ApiResponse<MyAttendanceStatusDto>>> GetMyAttendance()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var response = await _mediator.Send(new GetMyAttendanceQuery(userId));
        return Ok(response);
    }

    [HttpPost("vacations")]
    public async Task<IActionResult> SubmitVacation([FromBody] SubmitVacationRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var command = new SubmitVacationCommand(userId, request.StartDate, request.EndDate, request.Reason);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpGet("vacations/my")]
    public async Task<IActionResult> GetMyVacations()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var response = await _mediator.Send(new GetMyVacationsQuery(userId));
        return Ok(response);
    }
}

public record SubmitVacationRequest(DateOnly StartDate, DateOnly EndDate, string Reason);

