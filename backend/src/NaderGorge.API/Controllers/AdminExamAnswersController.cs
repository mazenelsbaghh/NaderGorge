using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Commands;

namespace NaderGorge.API.Controllers;

[ApiController, Authorize, Route("api/admin/exams")]
[HasPermission("content.manage")]
public sealed class AdminExamAnswersController(IMediator mediator) : ControllerBase
{
    public sealed record VisibilityRequest(bool ShowAnswers);

    [HttpPut("{examId:guid}/answer-visibility")]
    public async Task<IActionResult> SetVisibility(Guid examId, VisibilityRequest request, CancellationToken ct)
    {
        var response = await mediator.Send(new SetExamAnswerVisibilityCommand(examId, request.ShowAnswers, User.RequireUserId()), ct);
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        if (response.Errors?.Contains("FORBIDDEN") == true) return StatusCode(403, response);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
