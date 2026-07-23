using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Queries;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/hr")]
[Authorize]
public class AdminHrController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminHrController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("employees")]
    [HasPermission(HrPermissions.EmployeeRead)]
    public async Task<IActionResult> GetEmployees([FromQuery] string? search = null)
    {
        var response = await _mediator.Send(new AdminGetEmployeesQuery(search));
        return Ok(response);
    }

    [HttpPost("employees")]
    [HasPermission(HrPermissions.EmployeeManage)]
    public async Task<IActionResult> SaveEmployeeProfile([FromBody] AdminSaveEmployeeProfileCommand command)
    {
        var actorIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(actorIdValue, out var actorId))
        {
            return Unauthorized();
        }

        var response = await _mediator.Send(command with { ActorUserId = actorId });
        return Ok(response);
    }

    [HttpPost("employees/provision")]
    [HasPermission(HrPermissions.EmployeeManage)]
    public async Task<IActionResult> ProvisionEmployee(
        [FromBody] CreateEmployeeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var actorIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(actorIdValue, out var actorId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { message = "Idempotency-Key header is required" });
        }

        var response = await _mediator.Send(new CreateEmployeeCommand(
            request.FullName,
            request.PhoneNumber,
            request.Password,
            request.Role,
            request.BasicSalary,
            request.StandardStartTime,
            request.TargetDailyHours,
            actorId,
            idempotencyKey));
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("attendance")]
    [HasPermission(HrPermissions.AttendanceTeamRead)]
    public async Task<IActionResult> GetAttendance(
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var response = await _mediator.Send(new AdminGetAttendanceQuery(search, startDate, endDate));
        return Ok(response);
    }

}

public sealed record CreateEmployeeRequest(
    string FullName,
    string PhoneNumber,
    string Password,
    string Role,
    decimal BasicSalary,
    string StandardStartTime,
    int TargetDailyHours);
