using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.API.Extensions;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudentController(IMediator mediator) => _mediator = mediator;

    public sealed class UpdateStudentThemePreferencesRequest
    {
        public string LightPaletteId { get; set; } = string.Empty;
        public string DarkPaletteId { get; set; } = string.Empty;
        public string CurrentMode { get; set; } = "light";
        public string? AvatarSlug { get; set; }
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetDashboardQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("shell-bootstrap")]
    public async Task<IActionResult> GetShellBootstrap()
    {
        var result = await _mediator.Send(new GetShellBootstrapQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress()
    {
        var result = await _mediator.Send(new GetProgressQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("dashboard/quick-access")]
    public async Task<IActionResult> GetQuickAccess()
    {
        var result = await _mediator.Send(new GetQuickAccessQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("mistakes")]
    public async Task<IActionResult> GetMistakes()
    {
        var result = await _mediator.Send(new GetMistakesQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("theme-preferences")]
    public async Task<IActionResult> GetThemePreferences()
    {
        var result = await _mediator.Send(new GetStudentThemePreferencesQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("theme-preferences")]
    public async Task<IActionResult> UpdateThemePreferences([FromBody] UpdateStudentThemePreferencesRequest request)
    {
        var result = await _mediator.Send(
            new UpdateStudentThemePreferencesCommand(GetUserId(), request.LightPaletteId, request.DarkPaletteId, request.CurrentMode, request.AvatarSlug)
        );

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _mediator.Send(new GetStudentProfileQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateStudentProfileDto dto)
    {
        var result = await _mediator.Send(new UpdateStudentProfileCommand(
            GetUserId(),
            dto.Address,
            dto.SecondaryPhone,
            dto.ParentPhone,
            dto.SecondaryParentPhone,
            dto.MotherPhone,
            dto.SchoolName
        ));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var result = await _mediator.Send(new GetStudentNotificationsQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("notifications/{id}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(Guid id)
    {
        var result = await _mediator.Send(new MarkNotificationAsReadCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class UpdateStudentProfileDto
{
    public string Address { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string? ParentPhone { get; set; }
    public string? SecondaryParentPhone { get; set; }
    public string? MotherPhone { get; set; }
    public string? SchoolName { get; set; }
}
