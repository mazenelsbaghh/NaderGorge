using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assistant.Commands;
using NaderGorge.Application.Features.Assistant.Queries;
using NaderGorge.Domain.Entities.Assistant;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/assistant/tasks")]
public class AssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssistantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("pending")]
    [Authorize(Policy = "RequireAcademicAssistant")] // Assuming this or RequireAssistantReviewer
    public async Task<IActionResult> GetPendingTasks([FromQuery] AssistantTaskType? typeFilter, CancellationToken ct)
    {
        var query = new GetPendingTasksQuery(typeFilter);
        var result = await _mediator.Send(query, ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{taskId}/resolve")]
    [Authorize(Policy = "RequireAcademicAssistant")]
    public async Task<IActionResult> ResolveTask(Guid taskId, [FromBody] ResolveTaskRequest requestBody, CancellationToken ct)
    {
        var assistantIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(assistantIdClaim, out var assistantId))
            return Unauthorized(ApiResponse<Guid>.Fail("Invalid user token."));

        var command = new ResolveTaskCommand(taskId, assistantId, requestBody.ResolutionNotes);
        var result = await _mediator.Send(command, ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class ResolveTaskRequest
{
    public string ResolutionNotes { get; set; } = string.Empty;
}
