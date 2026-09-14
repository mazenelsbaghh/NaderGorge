using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.HR.Attendance.Commands;
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

        var key = Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        var command = new ClockInAttendanceCommand(userId, key, DateTime.UtcNow, null, null, null, null, ipAddress, userAgent);
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

        var key = Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        var command = new ClockOutAttendanceCommand(userId, key, DateTime.UtcNow);
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

}
