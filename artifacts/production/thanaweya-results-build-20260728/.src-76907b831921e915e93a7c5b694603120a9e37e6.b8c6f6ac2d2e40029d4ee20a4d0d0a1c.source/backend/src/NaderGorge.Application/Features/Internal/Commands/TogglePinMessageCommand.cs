using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record TogglePinMessageCommand(Guid MessageId, Guid UserId) : IRequest<ApiResponse>;

public class TogglePinMessageCommandHandler : IRequestHandler<TogglePinMessageCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public TogglePinMessageCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(TogglePinMessageCommand request, CancellationToken ct)
    {
        var message = await _db.ChatMessages
            .Include(m => m.ChatRoom)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, ct);

        if (message == null)
        {
            return ApiResponse.Fail("Message not found");
        }

        // Verify participant
        var isParticipant = await _db.ChatParticipants
            .AnyAsync(p => p.ChatRoomId == message.ChatRoomId && p.UserId == request.UserId, ct);

        if (!isParticipant)
        {
            return ApiResponse.Fail("You are not a participant in this room");
        }

        // Archive check
        if (message.ChatRoom.IsArchived)
        {
            var userRoles = await _db.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == request.UserId)
                .Select(ur => ur.Role.Type)
                .ToListAsync(ct);

            var isPrivileged = userRoles.Any(r => r == RoleType.Admin || r == RoleType.Supervisor);
            if (!isPrivileged)
            {
                return ApiResponse.Fail("This room is archived. Pins cannot be modified.");
            }
        }

        message.IsPinned = !message.IsPinned;
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}
