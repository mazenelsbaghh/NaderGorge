using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("{id:guid}/start")]
    public async Task<IActionResult> StartExam(Guid id)
    {
        var response = await _mediator.Send(new StartExamQuery(id, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors != null && response.Errors.Contains("You do not have access"))
                return StatusCode(403, response);

            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitExam(Guid id, [FromBody] List<AnswerSubmissionDto> answers)
    {
        var response = await _mediator.Send(new SubmitExamCommand(id, GetUserId(), answers));

        if (!response.Success)
            return BadRequest(response); // Exam not found handled via fail

        return Ok(response);
    }

    [HttpPost("admin/lessons/{lessonId:guid}/students/{studentId:guid}/unlock")]
    [Authorize(Roles = "Admin,Teacher,Assistant")]
    public async Task<IActionResult> ManualUnlock(Guid lessonId, Guid studentId)
    {
        var response = await _mediator.Send(new ManualUnlockCommand(lessonId, studentId, GetUserId()));

        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }
}
