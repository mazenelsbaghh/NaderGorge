using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Assistants.Queries;
using NaderGorge.Domain.Entities.Assistant;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/assistants")]
[Authorize(Roles = "Admin,Supervisor")]
public class AdminAssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminAssistantController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// GET /api/adminassistant/{id}/stats
    /// Returns aggregate profile stats for a specific assistant.
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAssistantProfileStatsQuery(id), ct));

    /// <summary>
    /// GET /api/adminassistant/{id}/tasks?page=1&amp;pageSize=20&amp;status=Open
    /// Returns paginated assistant tasks with optional status filter.
    /// </summary>
    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> GetTasks(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] AssistantTaskStatus? status = null,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetAssistantTasksQuery(id, page, pageSize, status), ct));

    /// <summary>
    /// GET /api/adminassistant/{id}/homework-reviews?page=1&amp;pageSize=20
    /// Returns paginated homework reviews for a specific assistant.
    /// </summary>
    [HttpGet("{id:guid}/homework-reviews")]
    public async Task<IActionResult> GetHomeworkReviews(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetAssistantHomeworkReviewsQuery(id, page, pageSize), ct));

    /// <summary>
    /// GET /api/adminassistant/{id}/warnings?page=1&amp;pageSize=20
    /// Returns paginated warnings resolved by a specific assistant.
    /// </summary>
    [HttpGet("{id:guid}/warnings")]
    public async Task<IActionResult> GetWarnings(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetAssistantWarningsQuery(id, page, pageSize), ct));
}
