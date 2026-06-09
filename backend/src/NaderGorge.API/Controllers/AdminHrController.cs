using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Queries;
using NaderGorge.API.Extensions;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/hr")]
[Authorize]
[HasPermission("hr.manage")]
public class AdminHrController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminHrController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees([FromQuery] string? search = null)
    {
        var response = await _mediator.Send(new AdminGetEmployeesQuery(search));
        return Ok(response);
    }

    [HttpPost("employees")]
    public async Task<IActionResult> SaveEmployeeProfile([FromBody] AdminSaveEmployeeProfileCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendance(
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var response = await _mediator.Send(new AdminGetAttendanceQuery(search, startDate, endDate));
        return Ok(response);
    }

    [HttpGet("vacations")]
    public async Task<IActionResult> GetVacations(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        var response = await _mediator.Send(new AdminGetVacationsQuery(search, status));
        return Ok(response);
    }

    [HttpPost("vacations/{id:guid}/approve")]
    public async Task<IActionResult> ApproveVacation([FromRoute] Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var adminUserId))
        {
            return Unauthorized();
        }

        var response = await _mediator.Send(new AdminApproveVacationCommand(id, adminUserId));
        return Ok(response);
    }

    [HttpPost("vacations/{id:guid}/reject")]
    public async Task<IActionResult> RejectVacation([FromRoute] Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var adminUserId))
        {
            return Unauthorized();
        }

        var response = await _mediator.Send(new AdminRejectVacationCommand(id, adminUserId));
        return Ok(response);
    }
}
