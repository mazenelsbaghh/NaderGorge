using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Gifts.Commands;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Features.Admin.Gifts.Queries;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/gifts")]
[Authorize]
[HasPermission("gifts.manage")]
public sealed class AdminGiftsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminGiftsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] GiftTargetType? targetType,
        [FromQuery] GiftIssuanceStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetGiftsQuery(search, targetType, status, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetGiftDetailsQuery(id), ct);
        return response.Errors?.Contains("NOT_FOUND") == true ? NotFound(response) : Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Issue([FromBody] IssueGiftRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new IssueGiftCommand(request, User.RequireUserId()), ct);
        if (!response.Success)
            return BadRequest(response);

        return response.Data?.IsReplay == true
            ? Ok(response)
            : StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeGiftRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new RevokeGiftCommand(id, request.Reason, User.RequireUserId()), ct);
        if (response.Errors?.Contains("NOT_FOUND") == true)
            return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("lookups/students")]
    public async Task<IActionResult> Students([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new GetGiftStudentsLookupQuery(search), ct));

    [HttpGet("lookups/teachers")]
    public async Task<IActionResult> Teachers([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new GetGiftTeachersLookupQuery(search), ct));

    [HttpGet("lookups/targets")]
    public async Task<IActionResult> Targets(
        [FromQuery] GiftTargetType targetType,
        [FromQuery] Guid? teacherId,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetGiftTargetsLookupQuery(targetType, teacherId, search), ct));
}

public sealed record RevokeGiftRequest(string Reason);
