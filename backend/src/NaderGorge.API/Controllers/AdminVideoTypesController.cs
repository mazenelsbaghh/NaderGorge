using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.VideoTypes.Commands;
using NaderGorge.Application.Features.Admin.VideoTypes.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/video-types")]
[Authorize]
public sealed class AdminVideoTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminVideoTypesController(IMediator mediator) => _mediator = mediator;

    private Guid UserId() => User.RequireUserId();

    [HttpGet]
    [HasPermission("content.manage")]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false)
        => Ok(await _mediator.Send(new GetVideoTypesQuery(includeInactive)));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateVideoTypeRequest request)
    {
        var response = await _mediator.Send(new CreateVideoTypeCommand(request.Name, request.SortOrder, request.IsActive, UserId()));
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVideoTypeRequest request)
    {
        var response = await _mediator.Send(new UpdateVideoTypeCommand(id, request.Name, request.SortOrder, UserId()));
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetVideoTypeStatusRequest request)
    {
        var response = await _mediator.Send(new SetVideoTypeStatusCommand(id, request.IsActive, UserId()));
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _mediator.Send(new DeleteVideoTypeCommand(id, UserId()));
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        if (response.Errors?.Contains("VIDEO_TYPE_IN_USE") == true) return Conflict(response);
        return response.Success ? NoContent() : BadRequest(response);
    }
}

public record CreateVideoTypeRequest(string Name, int SortOrder, bool IsActive = true);
public record UpdateVideoTypeRequest(string Name, int SortOrder);
public record SetVideoTypeStatusRequest(bool IsActive);
