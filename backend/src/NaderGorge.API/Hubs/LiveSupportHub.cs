using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using System.Collections.Concurrent;

namespace NaderGorge.API.Hubs;

[AllowAnonymous]
public sealed class LiveSupportHub(ILiveSupportService service, ILiveSupportPresenceStore presence, ILiveSupportGuestSessionService guestSessions) : Hub
{
    private static readonly ConcurrentDictionary<string, DateTime> TypingWindows = new();
    private Guid? StaffUserId => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        (Context.User!.IsInRole("Admin") || Context.User.IsInRole("Assistant") || Context.User.IsInRole("AssistantReviewer") || Context.User.IsInRole("Staff")) ? id : null;

    public override async Task OnConnectedAsync()
    {
        if (StaffUserId is { } staffId)
        {
            await service.GetStaffBootstrapAsync(staffId, Context.User!.IsInRole("Admin"), Context.ConnectionAborted);
            await presence.ConnectedAsync(staffId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"LiveSupport:Staff:{staffId:N}");
            if (service is ILiveSupportAssignmentCoordinator coordinator)
            {
                await coordinator.AssignWaitingAsync(Context.ConnectionAborted);
            }
        }
        else if (await ParticipantAsync() is { } participant)
            await Groups.AddToGroupAsync(Context.ConnectionId, participant.Type == LiveSupportParticipantType.Student
                ? $"LiveSupport:Participant:Student:{participant.StudentUserId:N}"
                : $"LiveSupport:Participant:Guest:{participant.GuestSessionId:N}");
        else Context.Abort();
        await base.OnConnectedAsync();
    }

    public async Task Heartbeat()
    {
        if (StaffUserId is { } staffId) await presence.HeartbeatAsync(staffId);
    }

    public async Task<object> JoinConversation(Guid conversationId)
    {
        try
        {
            LiveSupportParticipantIdentity? participantIdentity = null;
            if (StaffUserId is { } staffId) await service.GetStaffMessagesAsync(staffId, Context.User!.IsInRole("Admin"), conversationId, 1, Context.ConnectionAborted);
            else if (await ParticipantAsync() is { } participant)
            {
                if (await service.GetParticipantConversationAsync(participant, conversationId, Context.ConnectionAborted) is null) throw new HubException("NOT_PARTICIPANT");
                participantIdentity = participant;
            }
            else throw new HubException("SESSION_EXPIRED");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"LiveSupport:Conversation:{conversationId:N}");
            var lastEventSequence = StaffUserId is { } owner
                ? await service.GetStaffLastEventSequenceAsync(owner, Context.User!.IsInRole("Admin"), conversationId, Context.ConnectionAborted)
                : (await service.GetParticipantMessagePageAsync(participantIdentity!, conversationId, 1, null, null, Context.ConnectionAborted)).LastEventSequence;
            return new { conversationId, lastEventSequence };
        }
        catch (LiveSupportException ex) { throw new HubException(ex.Code == LiveSupportErrorCodes.Forbidden ? "NOT_OWNER" : ex.Code); }
    }

    public Task LeaveConversation(Guid conversationId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"LiveSupport:Conversation:{conversationId:N}");

    public async Task Typing(Guid conversationId)
    {
        var key = $"{Context.ConnectionId}:{conversationId:N}";
        var now = DateTime.UtcNow;
        if (TypingWindows.TryGetValue(key, out var prior) && now - prior < TimeSpan.FromMilliseconds(750)) throw new HubException("RATE_LIMITED");
        TypingWindows[key] = now;
        await JoinConversation(conversationId);
        await Clients.OthersInGroup($"LiveSupport:Conversation:{conversationId:N}").SendAsync("TypingChanged", new { conversationId, isTyping = true }, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (StaffUserId is { } staffId) await presence.DisconnectedAsync(staffId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<LiveSupportParticipantIdentity?> ParticipantAsync()
    {
        var idValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Context.User?.Identity?.IsAuthenticated == true && Context.User.IsInRole("Student") && Guid.TryParse(idValue, out var studentId))
            return new(LiveSupportParticipantType.Student, studentId, null);
        var cookie = Context.GetHttpContext()?.Request.Cookies["massar_support_guest"];
        return await guestSessions.ValidateAsync(cookie, Context.ConnectionAborted);
    }
}
