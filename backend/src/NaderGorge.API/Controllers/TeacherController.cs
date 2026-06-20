using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Teacher;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Roles = "Teacher")]
public class TeacherController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public TeacherController(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator;
        _db = db;
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

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity()
    {
        var result = await _mediator.Send(new GetTeacherActivityQuery(GetUserId()));
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
            dto.ProfileImageUrl,
            dto.AssistantPhoneNumbers,
            dto.FacebookUrl,
            dto.YouTubeUrl,
            dto.TelegramUrl
        ));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("profile/upload-image")]
    public async Task<IActionResult> UploadProfileImage([FromBody] UploadImageRequestDto dto)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps.UploadTeacherProfileImageCommand(GetUserId(), dto.Base64Image, dto.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("profile/upload-ai-photo")]
    public async Task<IActionResult> UploadAiPhoto([FromBody] UploadImageRequestDto dto)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps.UploadTeacherPhotoCommand(GetUserId(), dto.Base64Image, dto.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("profile/active-photo")]
    public async Task<IActionResult> GetMyActiveTeacherPhoto()
        => Ok(await _mediator.Send(new NaderGorge.Application.Features.Admin.Queries.GetActiveTeacherPhotoQuery(GetUserId())));

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
        => Ok(await _mediator.Send(new GetSubjectsQuery(GetUserId())));

    [HttpGet("codes/groups")]
    public async Task<IActionResult> ListCodeGroups()
        => Ok(await _mediator.Send(new ListCodeGroupsQuery(GetUserId())));

    [HttpGet("codes/groups/{id:guid}/details")]
    public async Task<IActionResult> GetCodeGroupDetails(Guid id)
    {
        var group = await _db.CodeGroups.FindAsync(id);
        if (group == null) return NotFound();

        var user = await _db.Users
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == GetUserId());

        if (user?.TeacherProfile == null || group.TeacherId != user.TeacherProfile.Id)
        {
            return Forbid();
        }

        var result = await _mediator.Send(new GetCodeGroupCodesQuery(id));
        return result.Success ? Ok(result) : NotFound(result);
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
    public string? AssistantPhoneNumbers { get; set; }
    public string? FacebookUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? TelegramUrl { get; set; }
}

public class UploadImageRequestDto
{
    public string Base64Image { get; set; } = null!;
    public string FileName { get; set; } = null!;
}
