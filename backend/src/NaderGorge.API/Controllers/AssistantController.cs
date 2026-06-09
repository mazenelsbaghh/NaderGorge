using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assistant.Commands;
using NaderGorge.Application.Features.Assistant.Queries;
using NaderGorge.Application.Features.Operations.Commands;
using NaderGorge.Application.Features.Operations.Queries;
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

    [HttpGet("my")]
    public async Task<IActionResult> GetMyTasks()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var response = await _mediator.Send(new GetMyTasksQuery(userId));
        return Ok(response);
    }

    [HttpGet("my/{id:guid}")]
    public async Task<IActionResult> GetTaskDetails(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var isAdminOrSupervisor = User.IsInRole("Admin") || User.IsInRole("Supervisor");

        var response = await _mediator.Send(new GetTaskDetailsQuery(id, userId, isAdminOrSupervisor));
        return Ok(response);
    }

    [HttpPost("my/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var response = await _mediator.Send(new UpdateTaskStatusCommand(id, request.Status, userId));
        return Ok(response);
    }

    [HttpPost("my/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var response = await _mediator.Send(new AddTaskCommentCommand(id, userId, request.Content, request.AttachmentUrl));
        return Ok(response);
    }
}

public class UpdateStatusRequest
{
    public NaderGorge.Domain.Enums.TaskStatus Status { get; set; }
}

public class AddCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}

public class ResolveTaskRequest
{
    public string ResolutionNotes { get; set; } = string.Empty;
}
