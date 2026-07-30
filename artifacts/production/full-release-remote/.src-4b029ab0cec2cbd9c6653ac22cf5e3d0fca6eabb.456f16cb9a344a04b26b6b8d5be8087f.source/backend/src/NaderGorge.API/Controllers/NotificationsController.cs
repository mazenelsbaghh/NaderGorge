using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

/// <summary>Personal in-app notifications for every authenticated platform user.</summary>
[ApiController, Route("api/notifications"), Authorize]
public sealed class NotificationsController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.NotificationEvents.AsNoTracking()
        .Where(item => item.UserId == User.RequireUserId())
        .OrderByDescending(item => item.CreatedAt).Take(100)
        .Select(item => new { item.Id, item.Title, item.Body, item.ReadAt, item.CreatedAt })
        .ToListAsync(ct));

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        var notification = await db.NotificationEvents.SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == User.RequireUserId(), ct);
        if (notification is null) return NotFound();
        if (notification.ReadAt is null) { notification.ReadAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        return NoContent();
    }
}
