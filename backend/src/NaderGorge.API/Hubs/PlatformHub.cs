using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Hubs;

[Authorize]
public class PlatformHub : Hub
{
    private readonly IAccessCheckService _accessCheckService;

    public PlatformHub(IAccessCheckService accessCheckService)
    {
        _accessCheckService = accessCheckService;
    }

    private Guid GetUserId()
    {
        var idClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    private string GetUserRole()
    {
        return Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            Context.Abort();
            return;
        }

        // Join personal user group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

        // Join role group
        var role = GetUserRole();
        if (!string.IsNullOrEmpty(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{role}");
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinPackage(string packageIdString)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty || !Guid.TryParse(packageIdString, out var packageId)) return;

        var role = GetUserRole();
        // Admins and teachers/assistants have direct access
        if (role == "Admin" || role == "Teacher" || role == "Assistant" || role == "AssistantReviewer" || role == "AssistantAcademic" || await _accessCheckService.HasAccessToPackageAsync(userId, packageId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Package_{packageId}");
        }
    }

    public async Task LeavePackage(string packageIdString)
    {
        if (Guid.TryParse(packageIdString, out var packageId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Package_{packageId}");
        }
    }

    public async Task JoinLesson(string lessonIdString)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty || !Guid.TryParse(lessonIdString, out var lessonId)) return;

        var role = GetUserRole();
        // Admins and teachers/assistants have direct access
        if (role == "Admin" || role == "Teacher" || role == "Assistant" || role == "AssistantReviewer" || role == "AssistantAcademic" || await _accessCheckService.HasAccessToLessonAsync(userId, lessonId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Lesson_{lessonId}");
        }
    }

    public async Task LeaveLesson(string lessonIdString)
    {
        if (Guid.TryParse(lessonIdString, out var lessonId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Lesson_{lessonId}");
        }
    }
}
