using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record MarkRoomReadCommand(Guid RoomId, Guid UserId) : IRequest<ApiResponse>;

public class MarkRoomReadCommandHandler : IRequestHandler<MarkRoomReadCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public MarkRoomReadCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(MarkRoomReadCommand request, CancellationToken ct)
    {
        var participant = await _db.ChatParticipants
            .FirstOrDefaultAsync(p => p.ChatRoomId == request.RoomId && p.UserId == request.UserId, ct);

        if (participant == null)
        {
            return ApiResponse.Fail("Participant not found in this chat room");
        }

        var latestMessage = await _db.ChatMessages
            .Where(m => m.ChatRoomId == request.RoomId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latestMessage != null)
        {
            participant.LastReadMessageId = latestMessage.Id;

            // Retrieve all messages in this room that don't have a read state for this user yet
            var alreadyReadMessageIds = await _db.ChatMessageReadStates
                .Where(rs => rs.UserId == request.UserId && rs.Message.ChatRoomId == request.RoomId)
                .Select(rs => rs.MessageId)
                .ToListAsync(ct);

            var unreadMessages = await _db.ChatMessages
                .Where(m => m.ChatRoomId == request.RoomId && !alreadyReadMessageIds.Contains(m.Id))
                .ToListAsync(ct);

            foreach (var msg in unreadMessages)
            {
                _db.ChatMessageReadStates.Add(new ChatMessageReadState
                {
                    MessageId = msg.Id,
                    UserId = request.UserId,
                    ReadAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        return ApiResponse.Ok();
    }
}
