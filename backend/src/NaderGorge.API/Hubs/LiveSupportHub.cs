using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using System.Collections.Concurrent;

namespace NaderGorge.API.Hubs;

[AllowAnonymous]
public sealed class LiveSupportHub(ILiveSupportService service, ILiveSupportPresenceStore presence, ILiveSupportGuestSessionService guestSessions, ILogger<LiveSupportHub> logger) : Hub
{
    private static readonly ConcurrentDictionary<string, DateTime> TypingWindows = new();
    private Guid? StaffUserId => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        (Context.User!.IsInRole("Admin") || Context.User.IsInRole("Assistant") || Context.User.IsInRole("AssistantReviewer") || Context.User.IsInRole("Staff")) ? id : null;

    public override async Task OnConnectedAsync()
    {
        try
        {
            if (StaffUserId is { } staffId)
            {
                await service.GetStaffBootstrapAsync(staffId, Context.User!.IsInRole("Admin"), Context.ConnectionAborted);
                await presence.ConnectedAsync(staffId, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"LiveSupport:Staff:{staffId:N}", Context.ConnectionAborted);
                if (service is ILiveSupportAssignmentCoordinator coordinator)
                {
                    try
                    {
                        await coordinator.AssignWaitingAsync(Context.ConnectionAborted);
                    }
                    catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Live support auto-assignment failed when staff {StaffUserId} connected", staffId);
                    }
                }
            }
            else if (await ParticipantAsync() is { } participant)
                await Groups.AddToGroupAsync(Context.ConnectionId, participant.Type == LiveSupportParticipantType.Student
                    ? $"LiveSupport:Participant:Student:{participant.StudentUserId:N}"
                    : $"LiveSupport:Participant:Guest:{participant.GuestSessionId:N}", Context.ConnectionAborted);
            else Context.Abort();
            await base.OnConnectedAsync();
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // Normal disconnect during bootstrap; suppress HubConnectionHandler fail logs.
        }
        catch (LiveSupportException exception) when (StaffUserId is not null)
        {
            logger.LogInformation("Live support connection rejected during staff bootstrap: {ErrorCode}", exception.Code);
            Context.Abort();
        }
    }

    public async Task Heartbeat()
    {
        if (StaffUserId is not { } staffId) return;
        await presence.HeartbeatAsync(staffId);
        if (service is ILiveSupportAssignmentCoordinator coordinator)
            await coordinator.AssignWaitingAsync(Context.ConnectionAborted);
    }

    public async Task<object> JoinConversation(Guid conversationId)
    {
        try
        {
            LiveSupportParticipantIdentity? participantIdentity = null;
            if (StaffUserId is { } staffId)
            {
                await service.GetStaffMessagesAsync(staffId, Context.User!.IsInRole("Admin"), conversationId, 1, Context.ConnectionAborted);
                await service.AcknowledgeParticipantMessagesAsync(conversationId, Context.ConnectionAborted);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"LiveSupport:ConversationStaff:{conversationId:N}", Context.ConnectionAborted);
            }
            else if (await ParticipantAsync() is { } participant)
            {
                if (await service.GetParticipantConversationAsync(participant, conversationId, Context.ConnectionAborted) is null) throw new HubException("NOT_PARTICIPANT");
                participantIdentity = participant;
                await service.AcknowledgeStaffMessagesAsync(conversationId, Context.ConnectionAborted);
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

    public async Task Typing(Guid conversationId, string? draft = null)
    {
        var key = $"{Context.ConnectionId}:{conversationId:N}";
        var now = DateTime.UtcNow;
        if (TypingWindows.TryGetValue(key, out var prior) && now - prior < TimeSpan.FromMilliseconds(750)) return;
        TypingWindows[key] = now;
        await JoinConversation(conversationId);
        if (StaffUserId is null)
        {
            var preview = string.IsNullOrWhiteSpace(draft) ? null : draft.Trim()[..Math.Min(draft.Trim().Length, 500)];
            await Clients.Group($"LiveSupport:ConversationStaff:{conversationId:N}").SendAsync("ParticipantTypingChanged", new { conversationId, isTyping = true, preview }, Context.ConnectionAborted);
            return;
        }
        await Clients.OthersInGroup($"LiveSupport:Conversation:{conversationId:N}").SendAsync("TypingChanged", new { conversationId, isTyping = true }, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var key in TypingWindows.Keys.Where(key => key.StartsWith($"{Context.ConnectionId}:", StringComparison.Ordinal)))
            TypingWindows.TryRemove(key, out _);
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
