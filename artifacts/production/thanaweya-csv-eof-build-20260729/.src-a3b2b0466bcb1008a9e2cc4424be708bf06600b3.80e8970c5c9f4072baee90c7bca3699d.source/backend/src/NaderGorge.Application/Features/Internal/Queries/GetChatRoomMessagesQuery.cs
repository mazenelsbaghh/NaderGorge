using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Queries;

public record GetChatRoomMessagesQuery(
    Guid RoomId,
    Guid UserId,
    int Page = 1,
    int PageSize = 50) : IRequest<ApiResponse<List<ChatMessageDto>>>;

public record ChatMessageDto(
    Guid Id,
    Guid RoomId,
    Guid SenderUserId,
    string SenderName,
    string Content,
    string Type,
    string? MediaUrl,
    string? MediaMetadata,
    bool IsPinned,
    DateTime CreatedAt,
    List<Guid> ReadBy);

public class GetChatRoomMessagesQueryHandler : IRequestHandler<GetChatRoomMessagesQuery, ApiResponse<List<ChatMessageDto>>>
{
    private readonly IAppDbContext _db;

    public GetChatRoomMessagesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<ChatMessageDto>>> Handle(GetChatRoomMessagesQuery request, CancellationToken ct)
    {
        // Verify user is participant
        var isParticipant = await _db.ChatParticipants
            .AnyAsync(p => p.ChatRoomId == request.RoomId && p.UserId == request.UserId, ct);

        if (!isParticipant)
        {
            return ApiResponse<List<ChatMessageDto>>.Fail("You are not authorized to view messages in this room");
        }

        var messages = await _db.ChatMessages
            .Include(m => m.SenderUser)
            .Where(m => m.ChatRoomId == request.RoomId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // Fetch read states for these messages
        var messageIds = messages.Select(m => m.Id).ToList();
        var readStates = await _db.ChatMessageReadStates
            .Where(rs => messageIds.Contains(rs.MessageId))
            .ToListAsync(ct);

        var result = messages.Select(m => new ChatMessageDto(
            m.Id,
            m.ChatRoomId,
            m.SenderUserId,
            m.SenderUser?.FullName ?? "Unknown",
            m.Content,
            m.Type.ToString(),
            m.MediaUrl,
            m.MediaMetadata,
            m.IsPinned,
            m.CreatedAt,
            readStates.Where(rs => rs.MessageId == m.Id).Select(rs => rs.UserId).ToList()
        )).OrderBy(m => m.CreatedAt).ToList(); // Order ascending for chat feed

        return ApiResponse<List<ChatMessageDto>>.Ok(result);
    }
}
