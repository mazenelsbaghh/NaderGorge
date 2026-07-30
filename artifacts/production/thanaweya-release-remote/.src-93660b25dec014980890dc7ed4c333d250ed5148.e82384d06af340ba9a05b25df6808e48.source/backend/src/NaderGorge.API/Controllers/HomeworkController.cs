using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Homework.Commands;
using NaderGorge.Application.Features.Homework.Queries;
using NaderGorge.API.Extensions;
using NaderGorge.API.Filters;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/homework")]
[Authorize(Roles = "Student")] // Or base Authorize depending on how roles work.
public class HomeworkController : ControllerBase
{
    private readonly IMediator _mediator;

    public HomeworkController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingHomework()
    {
        var result = await _mediator.Send(new GetPendingHomeworkQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{homeworkId}/submit")]
    [Idempotent]
    public async Task<IActionResult> SubmitHomework(Guid homeworkId, [FromBody] List<StudentAnswerInput> answers)
    {
        var command = new SubmitHomeworkCommand(homeworkId, GetUserId(), answers);
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{homeworkId}/start")]
    public async Task<IActionResult> StartHomework(Guid homeworkId)
    {
        var result = await _mediator.Send(new StartHomeworkAttemptQuery(homeworkId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{homeworkId}/result")]
    public async Task<IActionResult> GetHomeworkResult(Guid homeworkId)
    {
        var result = await _mediator.Send(new GetHomeworkResultQuery(homeworkId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

