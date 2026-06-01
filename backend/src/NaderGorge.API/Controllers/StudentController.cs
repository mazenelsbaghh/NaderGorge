using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using System.Security.Claims;

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

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetDashboardQuery(GetUserId()));
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
}
