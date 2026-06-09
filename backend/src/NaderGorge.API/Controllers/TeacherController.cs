using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Teacher;
using NaderGorge.Application.Features.Admin.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Roles = "Teacher")]
public class TeacherController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeacherController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var result = await _mediator.Send(new GetTeacherDashboardStatsQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("students")]
    public async Task<IActionResult> GetStudents()
    {
        var result = await _mediator.Send(new GetTeacherStudentsQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("essays")]
    public async Task<IActionResult> GetEssays()
    {
        var result = await _mediator.Send(new GetPendingTeacherEssaysQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("essays/{id}/grade")]
    public async Task<IActionResult> GradeEssay(Guid id, [FromBody] GradeEssayRequestDto dto)
    {
        var result = await _mediator.Send(new GradeEssayCommand(id, dto.Score, dto.Feedback, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _mediator.Send(new GetTeacherProfileQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] TeacherUpdateProfileRequestDto dto)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Teacher.UpdateTeacherProfileCommand(
            GetUserId(),
            dto.Bio,
            dto.Specialization,
            dto.ContactInfo,
            dto.ProfileImageUrl
        ));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class GradeEssayRequestDto
{
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
}

public class TeacherUpdateProfileRequestDto
{
    public string Bio { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
}
