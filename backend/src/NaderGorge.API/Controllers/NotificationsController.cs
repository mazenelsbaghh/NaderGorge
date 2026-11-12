using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.API.Extensions;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

/// <summary>Personal in-app notifications for every authenticated platform user.</summary>
[ApiController, Route("api/notifications"), Authorize]
public sealed class NotificationsController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await db.NotificationEvents.AsNoTracking()
            .Where(item => item.UserId == User.RequireUserId())
            .OrderByDescending(item => item.CreatedAt).Take(100)
            .Select(item => new { item.Id, item.Title, item.Body, item.ReadAt, item.CreatedAt,
                item.AcademicScopeOwnerType, item.AcademicScopeOwnerId })
            .ToListAsync(ct);
        IEnumerable<string> values = User.IsInRole("Student") ? rows.SelectMany(item => new[] { item.Title, item.Body }) : [];
        var links = await LessonYouTubeLinks.ResolveAsync(db, values, ct);
        return Ok(rows.Select(item =>
        {
            var lessonId = item.AcademicScopeOwnerType == StudentFacingScopeOwnerType.Lesson
                ? item.AcademicScopeOwnerId : null;
            return new { item.Id, Title = links.Rewrite(item.Title, lessonId), Body = links.Rewrite(item.Body, lessonId),
                item.ReadAt, item.CreatedAt };
        }));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        var notification = await db.NotificationEvents.SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == User.RequireUserId(), ct);
        if (notification is null) return NotFound();
        if (notification.ReadAt is null) { notification.ReadAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        return NoContent();
    }
}
