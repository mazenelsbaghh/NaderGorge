using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Internal.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Roles = "Admin,Supervisor,Assistant,Teacher,AssistantReviewer,AssistantAcademic,Staff")]
public class InternalChatController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalChatController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _mediator.Send(new GetChatRoomsQuery(userId), ct);
        return Ok(result);
    }

    [HttpGet("rooms/{roomId}/messages")]
    public async Task<IActionResult> GetRoomMessages(Guid roomId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _mediator.Send(new GetChatRoomMessagesQuery(roomId, userId, page, pageSize), ct);
        if (!result.Success) return Forbid();

        return Ok(result);
    }

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new CreateChatRoomCommand(request.Name, request.Type, request.ParticipantIds, userId, request.TaskItemId);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("rooms/{roomId}/archive")]
    public async Task<IActionResult> ArchiveRoom(Guid roomId, [FromBody] ArchiveRoomRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new ArchiveChatRoomCommand(roomId, userId, request.IsArchived);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("messages/{messageId}/pin")]
    public async Task<IActionResult> TogglePin(Guid messageId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new TogglePinMessageCommand(messageId, userId);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("rooms/{roomId}/read")]
    public async Task<IActionResult> MarkRead(Guid roomId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var command = new MarkRoomReadCommand(roomId, userId);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}

public record CreateRoomRequest(
    string? Name,
    Domain.Enums.ChatRoomType Type,
    List<Guid> ParticipantIds,
    Guid? TaskItemId = null);

public record ArchiveRoomRequest(bool IsArchived);
