using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Internal.Queries;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;

    public ChatHub(IAppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var idClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    private string GetUserName()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Someone";
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            Context.Abort();
            return;
        }

        // Join groups for all rooms the user is a participant of
        var roomIds = await _db.ChatParticipants
            .Where(p => p.UserId == userId)
            .Select(p => p.ChatRoomId)
            .ToListAsync();

        foreach (var roomId in roomIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
        }

        // Also join a personal group for targeted real-time notifications (e.g. mentions)
        await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

        await base.OnConnectedAsync();
    }

    public async Task SendMessage(Guid roomId, string content, string? mediaUrl = null, string? mediaMetadata = null)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var command = new SendChatMessageCommand(roomId, userId, content, Domain.Enums.ChatMessageType.Text, mediaUrl, mediaMetadata);
        var response = await _mediator.Send(command);

        if (response.Success)
        {
            // Fetch message details for broadcasting
            var messageId = response.Data;
            var message = await _db.ChatMessages
                .Include(m => m.SenderUser)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message != null)
            {
                var dto = new ChatMessageDto(
                    message.Id,
                    message.ChatRoomId,
                    message.SenderUserId,
                    message.SenderUser?.FullName ?? "Unknown",
                    message.Content,
                    message.Type.ToString(),
                    message.MediaUrl,
                    message.MediaMetadata,
                    message.IsPinned,
                    message.CreatedAt,
                    new List<Guid> { userId } // Initially read by sender
                );

                // Broadcast message to the room group
                await Clients.Group($"Room_{roomId}").SendAsync("ReceiveMessage", dto);

                // Check for mentions in the content to trigger a real-time notification alert to mentioned users
                var mentions = System.Text.RegularExpressions.Regex.Matches(content, @"@([\w\.\-]+)")
                    .Select(m => m.Groups[1].Value.Replace("_", " ").Trim())
                    .Distinct()
                    .ToList();

                if (mentions.Any())
                {
                    var targetUsers = await _db.Users
                        .Where(u => mentions.Contains(u.FullName) && u.Id != userId)
                        .Select(u => u.Id)
                        .ToListAsync();

                    foreach (var targetUserId in targetUsers)
                    {
                        // Send real-time notification to the mentioned user's personal group
                        await Clients.Group($"User_{targetUserId}").SendAsync("ReceiveNotification", new
                        {
                            Title = "Chat Mention",
                            Body = $"{GetUserName()} mentioned you in chat.",
                            RoomId = roomId
                        });
                    }
                }
            }
        }
        else
        {
            // Send error back to the caller
            await Clients.Caller.SendAsync("Error", response.Message);
        }
    }

    public async Task Typing(Guid roomId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var userName = GetUserName();
        // Broadcast typing state to room group (excluding the sender)
        await Clients.OthersInGroup($"Room_{roomId}").SendAsync("UserTyping", roomId, userId, userName);
    }
}
