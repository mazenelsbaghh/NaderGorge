using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Queries;

public record GetChatRoomsQuery(Guid UserId) : IRequest<ApiResponse<List<ChatRoomDto>>>;

public record ChatRoomDto(
    Guid Id,
    string Name,
    string Type,
    Guid? TaskItemId,
    bool IsArchived,
    DateTime CreatedAt,
    int UnreadCount,
    ChatMessagePreviewDto? LastMessage);

public record ChatMessagePreviewDto(
    Guid Id,
    string Content,
    string SenderName,
    DateTime CreatedAt);

public class GetChatRoomsQueryHandler : IRequestHandler<GetChatRoomsQuery, ApiResponse<List<ChatRoomDto>>>
{
    private readonly IAppDbContext _db;

    public GetChatRoomsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<ChatRoomDto>>> Handle(GetChatRoomsQuery request, CancellationToken ct)
    {
        // Get all rooms the user is a participant of
        var rooms = await _db.ChatRooms
            .Include(r => r.ChatParticipants)
                .ThenInclude(p => p.User)
            .Where(r => r.ChatParticipants.Any(p => p.UserId == request.UserId))
            .OrderByDescending(r => r.ChatMessages.Max(m => m.CreatedAt))
            .ToListAsync(ct);

        var result = new List<ChatRoomDto>();

        foreach (var room in rooms)
        {
            // Calculate unread messages
            var unreadCount = await _db.ChatMessages
                .CountAsync(m => m.ChatRoomId == room.Id &&
                                 m.SenderUserId != request.UserId &&
                                 !_db.ChatMessageReadStates.Any(rs => rs.MessageId == m.Id && rs.UserId == request.UserId), ct);

            // Fetch last message details
            var lastMessage = await _db.ChatMessages
                .Include(m => m.SenderUser)
                .Where(m => m.ChatRoomId == room.Id)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            ChatMessagePreviewDto? lastMessageDto = null;
            if (lastMessage != null)
            {
                lastMessageDto = new ChatMessagePreviewDto(
                    lastMessage.Id,
                    lastMessage.Content,
                    lastMessage.SenderUser?.FullName ?? "Unknown",
                    lastMessage.CreatedAt
                );
            }

            // Resolve name for direct chats
            string roomName = room.Name ?? string.Empty;
            if (room.Type == ChatRoomType.Individual)
            {
                var otherParticipant = room.ChatParticipants.FirstOrDefault(p => p.UserId != request.UserId);
                roomName = otherParticipant?.User?.FullName ?? "Direct Chat";
            }

            result.Add(new ChatRoomDto(
                room.Id,
                roomName,
                room.Type.ToString(),
                room.TaskItemId,
                room.IsArchived,
                room.CreatedAt,
                unreadCount,
                lastMessageDto
            ));
        }

        // Order rooms by last message date, fallback to room creation date
        var orderedResult = result
            .OrderByDescending(r => r.LastMessage?.CreatedAt ?? r.CreatedAt)
            .ToList();

        return ApiResponse<List<ChatRoomDto>>.Ok(orderedResult);
    }
}
