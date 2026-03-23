using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet("packages/{packageId:guid}/sections")]
    public async Task<IActionResult> GetSections(Guid packageId)
    {
        var response = await _mediator.Send(new GetSectionsQuery(packageId));
        return Ok(response);
    }

    [HttpGet("sections/{sectionId:guid}/lessons")]
    public async Task<IActionResult> GetLessons(Guid sectionId)
    {
        var response = await _mediator.Send(new GetLessonsQuery(sectionId, GetUserId()));
        
        if (!response.Success && response.Errors!.Contains("Section not found"))
            return NotFound(response);
            
        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<IActionResult> GetLessonDetail(Guid lessonId)
    {
        var response = await _mediator.Send(new GetLessonDetailQuery(lessonId, GetUserId()));
        
        if (!response.Success)
        {
            if (response.Errors!.Contains("You do not have access"))
                return StatusCode(403, response);
                
            return NotFound(response);
        }
            
        return Ok(response);
    }
}
