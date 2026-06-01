using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Community.Commands;
using NaderGorge.Application.Features.Community.Queries;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/community")]
[Authorize(Roles = "Student,Teacher,Admin,Assistant")]
public class CommunityController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommunityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts()
    {
        var response = await _mediator.Send(new GetCommunityPostsQuery(GetUserId()));
        return Ok(response);
    }

    [HttpGet("posts/mine")]
    public async Task<IActionResult> GetMyPosts()
    {
        var response = await _mediator.Send(new GetMyCommunityPostsQuery(GetUserId()));
        return Ok(response);
    }

    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost([FromBody] CreateCommunityPostRequest request)
    {
        var response = await _mediator.Send(new CreateCommunityPostCommand(GetUserId(), request.Body, request.PollOptions));
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("posts/{postId:guid}/comments")]
    public async Task<IActionResult> GetPostComments(Guid postId)
    {
        var response = await _mediator.Send(new GetCommunityPostCommentsQuery(postId, GetUserId()));
        if (!response.Success && response.Errors?.Contains("NOT_FOUND") == true)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost("posts/{postId:guid}/comments")]
    public async Task<IActionResult> CreatePostComment(Guid postId, [FromBody] CreateCommunityCommentRequest request)
    {
        var response = await _mediator.Send(new CreateCommunityPostCommentCommand(postId, GetUserId(), request.Body));
        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("posts/{postId:guid}/likes/toggle")]
    public async Task<IActionResult> ToggleLike(Guid postId)
    {
        var response = await _mediator.Send(new ToggleCommunityPostLikeCommand(postId, GetUserId()));
        if (!response.Success && response.Errors?.Contains("NOT_FOUND") == true)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost("posts/{postId:guid}/polls/{optionId:guid}/vote")]
    public async Task<IActionResult> ToggleVote(Guid postId, Guid optionId)
    {
        var response = await _mediator.Send(new ToggleCommunityPostVoteCommand(postId, optionId, GetUserId()));
        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);
                
            return BadRequest(response);
        }

        return Ok(response);
    }
}

public record CreateCommunityPostRequest(string Body, List<string>? PollOptions = null);
public record CreateCommunityCommentRequest(string Body);
