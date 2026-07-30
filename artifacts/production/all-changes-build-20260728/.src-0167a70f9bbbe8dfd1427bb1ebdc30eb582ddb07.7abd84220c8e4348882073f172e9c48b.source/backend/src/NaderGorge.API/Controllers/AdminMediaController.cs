using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Media.Commands;
using NaderGorge.Application.Features.Admin.Media.Queries;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/media")]
[Authorize]
public class AdminMediaController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminMediaController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    [HttpGet("pipelines")]
    [HasPermission("media.manage")]
    public async Task<IActionResult> GetPipelines(
        [FromQuery] string? search = null,
        [FromQuery] MediaStage? stage = null,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetMediaPipelinesQuery(search, stage, assigneeId, page, pageSize);
        var response = await _mediator.Send(query, ct);
        return Ok(response);
    }

    [HttpPost("pipelines")]
    [HasPermission("media.manage")]
    public async Task<IActionResult> CreatePipeline([FromBody] CreatePipelineRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new CreateMediaPipelineCommand(request.Title, request.Description, request.AssignedAgentId, request.AssetFolderUrl, userId);
        var response = await _mediator.Send(command, ct);
        
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpPut("pipelines/{id:guid}")]
    [HasPermission("media.manage")]
    public async Task<IActionResult> UpdatePipeline(Guid id, [FromBody] UpdatePipelineRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new UpdateMediaPipelineCommand(
            id,
            userId,
            request.Title,
            request.Description,
            request.AssignedAgentId,
            request.AssetFolderUrl,
            request.EditingErrorCount,
            request.Stage,
            request.SupervisorId
        );

        var response = await _mediator.Send(command, ct);
        
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet("social-plans")]
    [HasPermission("media.manage")]
    public async Task<IActionResult> GetSocialPlans(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var query = new GetSocialPlansQuery(startDate, endDate);
        var response = await _mediator.Send(query, ct);
        return Ok(response);
    }

    [HttpPost("social-plans")]
    [HasPermission("media.manage")]
    public async Task<IActionResult> CreateSocialPlan([FromBody] CreateSocialPlanRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new CreateSocialPlanCommand(
            request.Title,
            request.Description,
            request.Script,
            request.Platform,
            request.Status,
            request.ScheduledDate,
            request.MediaProductionPipelineId,
            userId
        );
        var response = await _mediator.Send(command, ct);

        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet("reports/kpis")]
    [HasPermission("media.manage")]
    public async Task<IActionResult> GetMediaKpis(CancellationToken ct)
    {
        var query = new GetMediaKpisQuery();
        var response = await _mediator.Send(query, ct);
        return Ok(response);
    }
}

public record CreatePipelineRequest(
    string Title,
    string? Description,
    Guid? AssignedAgentId,
    string? AssetFolderUrl
);

public record UpdatePipelineRequest(
    string Title,
    string? Description,
    Guid? AssignedAgentId,
    string? AssetFolderUrl,
    int EditingErrorCount,
    MediaStage Stage,
    Guid? SupervisorId
);

public record CreateSocialPlanRequest(
    string Title,
    string? Description,
    string? Script,
    SocialPlatform Platform,
    SocialPlanStatus Status,
    DateTime ScheduledDate,
    Guid? MediaProductionPipelineId
);
