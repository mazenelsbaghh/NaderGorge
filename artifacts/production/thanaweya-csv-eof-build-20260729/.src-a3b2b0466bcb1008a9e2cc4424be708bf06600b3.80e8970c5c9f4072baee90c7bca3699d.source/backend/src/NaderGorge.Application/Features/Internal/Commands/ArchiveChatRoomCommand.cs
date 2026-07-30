using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record ArchiveChatRoomCommand(Guid RoomId, Guid UserId, bool IsArchived) : IRequest<ApiResponse>;

public class ArchiveChatRoomCommandHandler : IRequestHandler<ArchiveChatRoomCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public ArchiveChatRoomCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(ArchiveChatRoomCommand request, CancellationToken ct)
    {
        var room = await _db.ChatRooms.FindAsync(new object[] { request.RoomId }, ct);
        if (room == null)
        {
            return ApiResponse.Fail("Chat room not found");
        }

        // Verify that the user is Admin or Supervisor
        var userRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == request.UserId)
            .Select(ur => ur.Role.Type)
            .ToListAsync(ct);

        var isPrivileged = userRoles.Any(r => r == RoleType.Admin || r == RoleType.Supervisor);
        if (!isPrivileged)
        {
            return ApiResponse.Fail("Only administrators or supervisors can archive chat rooms.");
        }

        room.IsArchived = request.IsArchived;
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}
