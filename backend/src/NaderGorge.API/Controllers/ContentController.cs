using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Content.Commands;
using NaderGorge.Application.Features.Content.Queries;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContentController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages()
    {
        var response = await _mediator.Send(new GetPackagesQuery(GetUserId()));
        if (!response.Success) return BadRequest(response); // Shouldn't happen for packages

        return Ok(response);
    }

    [HttpGet("packages/{packageId:guid}/terms")]
    public async Task<IActionResult> GetTerms(Guid packageId)
    {
        var response = await _mediator.Send(new GetTermsQuery(packageId));
        return Ok(response);
    }

    [HttpGet("packages/{packageId:guid}/code-page")]
    public async Task<IActionResult> GetPackageCodePage(Guid packageId)
    {
        var response = await _mediator.Send(new GetPackageCodePageQuery(packageId));
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpGet("terms/{termId:guid}/sections")]
    public async Task<IActionResult> GetSections(Guid termId)
    {
        var response = await _mediator.Send(new GetSectionsQuery(termId));
        return Ok(response);
    }

    [HttpGet("sections/{sectionId:guid}/lessons")]
    public async Task<IActionResult> GetLessons(Guid sectionId)
    {
        var response = await _mediator.Send(new GetLessonsQuery(sectionId, GetUserId()));

        if (!response.Success && (response.Errors?.Contains("Section not found") == true || response.Message?.Contains("Section not found") == true))
            return NotFound(response);

        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<IActionResult> GetLessonDetail(Guid lessonId)
    {
        var response = await _mediator.Send(new GetLessonDetailQuery(lessonId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("You do not have access") == true || response.Message?.Contains("You do not have access") == true)
                return StatusCode(403, response);

            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}/comments")]
    public async Task<IActionResult> GetLessonComments(Guid lessonId)
    {
        var response = await _mediator.Send(new GetLessonCommentsQuery(lessonId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("FORBIDDEN") == true)
                return StatusCode(403, response);

            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}/comments/mine")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyLessonComments(Guid lessonId)
    {
        var response = await _mediator.Send(new GetMyLessonCommentsQuery(lessonId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("FORBIDDEN") == true)
                return StatusCode(403, response);

            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("lessons/{lessonId:guid}/comments")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateLessonComment(Guid lessonId, [FromBody] CreateLessonCommentRequest request)
    {
        var response = await _mediator.Send(new CreateLessonCommentCommand(lessonId, GetUserId(), request.Body));

        if (!response.Success)
        {
            if (response.Errors?.Contains("FORBIDDEN") == true)
                return StatusCode(403, response);

            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }
}

public record CreateLessonCommentRequest(string Body);
