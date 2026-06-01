using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Teacher")]
public class AdminLessonCommentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminLessonCommentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    [HttpGet("lessons/{lessonId:guid}/comments")]
    public async Task<IActionResult> GetLessonCommentsForModeration(Guid lessonId, [FromQuery] string? status = null)
    {
        var response = await _mediator.Send(new GetLessonCommentsForModerationQuery(lessonId, status));

        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            if (response.Errors?.Contains("INVALID_STATUS") == true)
                return BadRequest(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("comments/{commentId:guid}/approve")]
    public async Task<IActionResult> ApproveLessonComment(Guid commentId)
    {
        var response = await _mediator.Send(new ApproveLessonCommentCommand(commentId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("comments/{commentId:guid}/reject")]
    public async Task<IActionResult> RejectLessonComment(Guid commentId)
    {
        var response = await _mediator.Send(new RejectLessonCommentCommand(commentId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }
}
