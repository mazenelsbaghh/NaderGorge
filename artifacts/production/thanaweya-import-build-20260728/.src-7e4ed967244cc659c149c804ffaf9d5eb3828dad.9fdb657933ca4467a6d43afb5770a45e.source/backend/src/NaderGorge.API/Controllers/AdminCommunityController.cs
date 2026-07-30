using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.API.Extensions;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/community")]
[Authorize]
[HasPermission("community.manage")]
public class AdminCommunityController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminCommunityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("posts")]
    public async Task<IActionResult> GetPostsForModeration([FromQuery] string? status = null)
    {
        var response = await _mediator.Send(new GetCommunityPostsForModerationQuery(status));
        if (!response.Success && response.Errors?.Contains("INVALID_STATUS") == true)
            return BadRequest(response);
        return Ok(response);
    }

    [HttpPost("posts/{postId:guid}/approve")]
    public async Task<IActionResult> ApprovePost(Guid postId)
    {
        var response = await _mediator.Send(new ApproveCommunityPostCommand(postId, GetUserId()));
        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("posts/{postId:guid}/reject")]
    public async Task<IActionResult> RejectPost(Guid postId)
    {
        var response = await _mediator.Send(new RejectCommunityPostCommand(postId, GetUserId()));
        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("comments/pending")]
    public async Task<IActionResult> GetPendingComments()
    {
        var response = await _mediator.Send(new GetCommunityCommentsForModerationQuery());
        return Ok(response);
    }

    [HttpPost("comments/{commentId:guid}/approve")]
    public async Task<IActionResult> ApproveComment(Guid commentId)
    {
        var response = await _mediator.Send(new ApproveCommunityCommentCommand(commentId, GetUserId()));
        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("comments/{commentId:guid}/reject")]
    public async Task<IActionResult> RejectComment(Guid commentId, [FromBody] RejectCommunityCommentRequest request)
    {
        var response = await _mediator.Send(new RejectCommunityCommentCommand(commentId, GetUserId(), request.Reason ?? string.Empty));
        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }
}

public class RejectCommunityCommentRequest
{
    public string? Reason { get; set; }
}
