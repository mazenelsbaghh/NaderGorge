using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Internal.Queries;
using NaderGorge.Application.Services;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Roles = "Admin,Supervisor,Assistant,Teacher,AssistantReviewer,AssistantAcademic,Staff")]
public class InternalChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TeacherAuthorizationService _teacherAuthorization;
    private readonly IAppDbContext _db;

    public InternalChatController(IMediator mediator, TeacherAuthorizationService teacherAuthorization, IAppDbContext db)
    {
        _mediator = mediator;
        _teacherAuthorization = teacherAuthorization;
        _db = db;
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
        if (!await CanAccessChatAsync(userId, ct)) return Forbid();

        var result = await _mediator.Send(new GetChatRoomsQuery(userId), ct);
        return Ok(result);
    }

    [HttpGet("rooms/{roomId}/messages")]
    public async Task<IActionResult> GetRoomMessages(Guid roomId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await CanAccessChatAsync(userId, ct)) return Forbid();

        var result = await _mediator.Send(new GetChatRoomMessagesQuery(roomId, userId, page, pageSize), ct);
        if (!result.Success) return Forbid();

        return Ok(result);
    }

    [HttpPost("rooms")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await CanAccessChatAsync(userId, ct)) return Forbid();

        var command = new CreateChatRoomCommand(request.Name, request.Type, request.ParticipantIds, userId, request.TaskItemId);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("rooms/{roomId:guid}/members")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMembers(Guid roomId, CancellationToken ct)
    {
        if (!await _db.ChatParticipants.AnyAsync(p => p.ChatRoomId == roomId && p.UserId == GetUserId(), ct)) return Forbid();
        return Ok(await _db.ChatParticipants.Where(p => p.ChatRoomId == roomId).Select(p => p.UserId).ToListAsync(ct));
    }

    [HttpPut("rooms/{roomId:guid}/members")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateMembers(Guid roomId, [FromBody] List<Guid> memberIds, CancellationToken ct)
    {
        var room = await _db.ChatRooms.Include(r => r.ChatParticipants).SingleOrDefaultAsync(r => r.Id == roomId, ct);
        if (room == null) return NotFound();
        if (!room.ChatParticipants.Any(p => p.UserId == GetUserId())) return Forbid();
        if (room.Type == Domain.Enums.ChatRoomType.Individual) return BadRequest();
        var ids = memberIds.Append(GetUserId()).Distinct().ToList();
        if (ids.Count > 100 || await _db.Users.CountAsync(u => ids.Contains(u.Id) && u.IsActive && !u.IsDeleted &&
            u.UserRoles.Any(r => r.Role.Type != Domain.Enums.RoleType.Student), ct) != ids.Count) return BadRequest();
        _db.ChatParticipants.RemoveRange(room.ChatParticipants.Where(p => !ids.Contains(p.UserId)).ToList());
        foreach (var id in ids.Where(id => !room.ChatParticipants.Any(p => p.UserId == id)))
            room.ChatParticipants.Add(new ChatParticipant { UserId = id });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("rooms/{roomId}/archive")]
    public async Task<IActionResult> ArchiveRoom(Guid roomId, [FromBody] ArchiveRoomRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await CanAccessChatAsync(userId, ct)) return Forbid();

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
        if (!await CanAccessChatAsync(userId, ct)) return Forbid();

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
        if (!await CanAccessChatAsync(userId, ct)) return Forbid();

        var command = new MarkRoomReadCommand(roomId, userId);
        var result = await _mediator.Send(command, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    private async Task<bool> CanAccessChatAsync(Guid userId, CancellationToken ct)
    {
        if (User.IsInRole("Admin") || User.Claims.Any(c =>
                c.Type == "permission" && c.Value.Equals("chat.manage", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return await _teacherAuthorization.CanAccessTeacherWorkspacePermissionAsync(userId, "chat", ct);
    }
}

public record CreateRoomRequest(
    string? Name,
    Domain.Enums.ChatRoomType Type,
    List<Guid> ParticipantIds,
    Guid? TaskItemId = null);

public record ArchiveRoomRequest(bool IsArchived);
