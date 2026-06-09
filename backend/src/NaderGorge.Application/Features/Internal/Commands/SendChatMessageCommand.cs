using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record SendChatMessageCommand(
    Guid RoomId,
    Guid SenderUserId,
    string Content,
    ChatMessageType Type = ChatMessageType.Text,
    string? MediaUrl = null,
    string? MediaMetadata = null) : IRequest<ApiResponse<Guid>>;

public class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public SendChatMessageCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(SendChatMessageCommand request, CancellationToken ct)
    {
        // 1. Verify room exists and sender is participant
        var room = await _db.ChatRooms
            .Include(r => r.ChatParticipants)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, ct);

        if (room == null)
        {
            return ApiResponse<Guid>.Fail("Chat room not found");
        }

        var isParticipant = room.ChatParticipants.Any(p => p.UserId == request.SenderUserId);
        if (!isParticipant)
        {
            return ApiResponse<Guid>.Fail("You are not a participant in this chat room");
        }

        // 2. Archive Check: if archived, only Admin/Supervisor can post
        if (room.IsArchived)
        {
            var userRoles = await _db.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == request.SenderUserId)
                .Select(ur => ur.Role.Type)
                .ToListAsync(ct);

            var isPrivileged = userRoles.Any(r => r == RoleType.Admin || r == RoleType.Supervisor);
            if (!isPrivileged)
            {
                return ApiResponse<Guid>.Fail("This room is archived. New messages cannot be sent.");
            }
        }

        // 3. Create message
        var message = new ChatMessage
        {
            ChatRoomId = request.RoomId,
            SenderUserId = request.SenderUserId,
            Content = request.Content?.Trim() ?? string.Empty,
            Type = request.Type,
            MediaUrl = request.MediaUrl,
            MediaMetadata = request.MediaMetadata,
            CreatedAt = DateTime.UtcNow
        };

        _db.ChatMessages.Add(message);

        // 4. Update Sender's Read State automatically
        var senderParticipant = room.ChatParticipants.First(p => p.UserId == request.SenderUserId);
        senderParticipant.LastReadMessageId = message.Id;

        // 5. Parse Mentions and Raise Notifications
        var sender = await _db.Users.FindAsync(new object[] { request.SenderUserId }, ct);
        var senderName = sender?.FullName ?? "Someone";

        var mentions = Regex.Matches(message.Content, @"@([\w\.\-]+)")
            .Select(m => m.Groups[1].Value.Replace("_", " ").Trim())
            .Distinct()
            .ToList();

        if (mentions.Any())
        {
            var targetUsers = await _db.Users
                .Where(u => mentions.Contains(u.FullName) && u.Id != request.SenderUserId)
                .Select(u => u.Id)
                .ToListAsync(ct);

            foreach (var targetUserId in targetUsers)
            {
                _db.NotificationEvents.Add(new NotificationEvent
                {
                    UserId = targetUserId,
                    ChannelType = NotificationChannelType.InApp,
                    Title = "Chat Mention",
                    Body = $"{senderName} mentioned you in chat.",
                    Status = NotificationStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(message.Id);
    }
}
