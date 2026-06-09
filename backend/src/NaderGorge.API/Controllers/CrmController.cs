using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.CRM.Commands;
using NaderGorge.Application.Features.CRM.Queries;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/crm")]
[Authorize(Roles = "Admin,Supervisor,Assistant,Teacher,Staff")]
public class CrmController : ControllerBase
{
    private readonly IMediator _mediator;

    public CrmController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    [HttpGet("students")]
    public async Task<IActionResult> GetStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] CrmStatus? status = null,
        [FromQuery] Guid? agentId = null,
        [FromQuery] CrmPriority? priority = null,
        [FromQuery] bool onlyOverdue = false,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var query = new GetCrmStudentsQuery(userId, page, pageSize, search, status, agentId, priority, onlyOverdue);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("students/{studentId}/assign")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> AssignStudent(Guid studentId, [FromBody] AssignStudentRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new AssignStudentToAgentCommand(studentId, request.AssignedAgentId, request.Priority, request.Notes, userId);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("students/{studentId}/calls")]
    public async Task<IActionResult> LogCall(Guid studentId, [FromBody] LogCallRequest request, CancellationToken ct)
    {
        var agentId = GetUserId();
        if (agentId == Guid.Empty) return Unauthorized();

        var command = new LogCrmCallCommand(studentId, agentId, request.Outcome, request.Notes, request.NextFollowUpDate);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("students/{studentId}/history")]
    public async Task<IActionResult> GetHistory(Guid studentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var query = new GetCrmStudentHistoryQuery(studentId, userId);
        var result = await _mediator.Send(query, ct);

        if (!result.Success) return Forbid();
        return Ok(result);
    }

    [HttpGet("reports/performance")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> GetPerformanceReport(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var query = new GetCrmPerformanceReportQuery(userId);
        var result = await _mediator.Send(query, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}

public record AssignStudentRequest(Guid? AssignedAgentId, CrmPriority Priority, string? Notes);

public record LogCallRequest(CallOutcome Outcome, string? Notes, DateTime? NextFollowUpDate);
