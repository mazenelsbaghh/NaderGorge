using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using System.Security.Claims;
using NaderGorge.API.Filters;
using NaderGorge.API.Extensions;

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

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartExamAttempt(Guid id)
    {
        var response = await _mediator.Send(new StartExamAttemptCommand(id, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors != null && response.Errors.Contains("You do not have access"))
                return StatusCode(403, response);

            if (response.Errors != null && response.Errors.Contains("Exam not found"))
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}/latest-passed-result")]
    public async Task<IActionResult> GetLatestPassedResult(Guid id)
    {
        var response = await _mediator.Send(new GetLatestPassedExamResultQuery(id, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors != null && response.Errors.Contains("You do not have access"))
                return StatusCode(403, response);

            if (response.Errors != null && response.Errors.Contains("Exam not found"))
                return NotFound(response);

            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpPost("{id:guid}/submit/{attemptId:guid}")]
    [Idempotent]
    public async Task<IActionResult> SubmitExam(Guid id, Guid attemptId, [FromBody] List<AnswerSubmissionDto> answers)
    {
        var response = await _mediator.Send(new SubmitExamCommand(id, attemptId, GetUserId(), answers));

        if (!response.Success)
            return BadRequest(response); // Exam not found handled via fail

        return Ok(response);
    }

    [HttpGet("attempts/{attemptId:guid}/grading-status")]
    public async Task<IActionResult> GetGradingStatus(Guid attemptId)
    {
        var response = await _mediator.Send(new GetExamAttemptGradingStatusQuery(attemptId, GetUserId()));
        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}/attempts/{attemptId:guid}/questions/{questionId:guid}/fifty-fifty")]
    public async Task<IActionResult> UseFiftyFifty(Guid id, Guid attemptId, Guid questionId)
    {
        var response = await _mediator.Send(new UseFiftyFiftyCommand(id, attemptId, questionId, GetUserId()));

        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPost("{id:guid}/attempts/{attemptId:guid}/questions/{questionId:guid}/swap")]
    public async Task<IActionResult> SwapQuestion(Guid id, Guid attemptId, Guid questionId)
    {
        var response = await _mediator.Send(new SwapQuestionCommand(id, attemptId, questionId, GetUserId()));

        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPost("admin/lessons/{lessonId:guid}/students/{studentId:guid}/unlock")]
    [HasPermission("watch_requests.manage")]
    public async Task<IActionResult> ManualUnlock(Guid lessonId, Guid studentId)
    {
        var response = await _mediator.Send(new ManualUnlockCommand(lessonId, studentId, GetUserId()));

        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }
}
