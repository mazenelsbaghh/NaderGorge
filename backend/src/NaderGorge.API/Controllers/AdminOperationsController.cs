using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Operations.Commands;
using NaderGorge.Application.Features.Operations.Queries;
using NaderGorge.API.Extensions;
using NaderGorge.Domain.Enums;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/operations")]
[Authorize]
[HasPermission("hr.manage")]
public class AdminOperationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminOperationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks(
        [FromQuery] string? search = null,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] TaskStatus? status = null,
        [FromQuery] TaskPriority? priority = null)
    {
        var response = await _mediator.Send(new GetAdminTasksQuery(search, assigneeId, status, priority));
        return Ok(response);
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var adminUserId))
        {
            return Unauthorized();
        }

        var command = new CreateTaskCommand(
            request.Title,
            request.Description ?? string.Empty,
            request.AssigneeId,
            request.Priority,
            request.DueDate,
            adminUserId
        );

        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpPost("tasks/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveApproval(Guid id, [FromBody] ResolveApprovalRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var adminUserId))
        {
            return Unauthorized();
        }

        var command = new AdminResolveApprovalCommand(id, adminUserId, request.Approve, request.RejectionReason);
        var response = await _mediator.Send(command);
        return Ok(response);
    }
}

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssigneeId { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
}

public class ResolveApprovalRequest
{
    public bool Approve { get; set; }
    public string? RejectionReason { get; set; }
}
