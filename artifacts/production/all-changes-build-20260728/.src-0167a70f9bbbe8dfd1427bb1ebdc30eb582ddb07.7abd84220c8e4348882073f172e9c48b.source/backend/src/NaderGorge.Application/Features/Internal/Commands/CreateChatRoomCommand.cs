using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record CreateChatRoomCommand(
    string? Name,
    ChatRoomType Type,
    List<Guid> ParticipantIds,
    Guid CurrentUserId,
    Guid? TaskItemId = null) : IRequest<ApiResponse<Guid>>;

public class CreateChatRoomCommandHandler : IRequestHandler<CreateChatRoomCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateChatRoomCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateChatRoomCommand request, CancellationToken ct)
    {
        // 1. Authorization: Verify that all participants exist
        var allUserIds = request.ParticipantIds.Concat(new[] { request.CurrentUserId }).Distinct().ToList();
        var existingUsersCount = await _db.Users.CountAsync(u => allUserIds.Contains(u.Id), ct);
        if (existingUsersCount != allUserIds.Count)
        {
            return ApiResponse<Guid>.Fail("One or more user participants not found");
        }

        // 2. Direct Chat Uniqueness
        if (request.Type == ChatRoomType.Individual)
        {
            if (request.ParticipantIds.Count != 1)
            {
                return ApiResponse<Guid>.Fail("Individual direct chats must have exactly one other participant");
            }

            var otherUserId = request.ParticipantIds.First();
            if (otherUserId == request.CurrentUserId)
            {
                return ApiResponse<Guid>.Fail("Cannot create direct chat with yourself");
            }

            var existingRoomId = await _db.ChatRooms
                .Where(r => r.Type == ChatRoomType.Individual && !r.IsArchived)
                .Where(r => r.ChatParticipants.Any(p => p.UserId == request.CurrentUserId) &&
                            r.ChatParticipants.Any(p => p.UserId == otherUserId))
                .Select(r => r.Id)
                .FirstOrDefaultAsync(ct);

            if (existingRoomId != Guid.Empty)
            {
                return ApiResponse<Guid>.Ok(existingRoomId);
            }
        }

        // 3. Create room
        var room = new ChatRoom
        {
            Name = request.Type == ChatRoomType.Individual ? null : (request.Name?.Trim() ?? "Group Chat"),
            Type = request.Type,
            TaskItemId = request.TaskItemId,
            CreatedByUserId = request.CurrentUserId
        };

        foreach (var userId in allUserIds)
        {
            room.ChatParticipants.Add(new ChatParticipant
            {
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });
        }

        _db.ChatRooms.Add(room);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(room.Id);
    }
}
