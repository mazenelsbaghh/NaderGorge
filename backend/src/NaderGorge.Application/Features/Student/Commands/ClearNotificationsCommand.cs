using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Student.Commands;

public record ClearNotificationsCommand(Guid UserId) : IRequest<ApiResponse<bool>>;

public class ClearNotificationsCommandHandler : IRequestHandler<ClearNotificationsCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public ClearNotificationsCommandHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<bool>> Handle(ClearNotificationsCommand request, CancellationToken ct)
    {
        var unreadNotifications = await _db.NotificationEvents
            .Where(n => n.UserId == request.UserId && n.ReadAt == null)
            .ToListAsync(ct);

        if (_academicScope != null)
        {
            var eligibleNotifications = new List<NaderGorge.Domain.Entities.Notifications.NotificationEvent>(unreadNotifications.Count);
            foreach (var notification in unreadNotifications)
            {
                if (!notification.AcademicScopeOwnerType.HasValue || !notification.AcademicScopeOwnerId.HasValue)
                {
                    eligibleNotifications.Add(notification);
                    continue;
                }

                if (await _academicScope.IsOwnerEligibleForStudentAsync(
                        notification.AcademicScopeOwnerType.Value,
                        notification.AcademicScopeOwnerId.Value,
                        request.UserId,
                        ct))
                {
                    eligibleNotifications.Add(notification);
                }
            }

            unreadNotifications = eligibleNotifications;
        }

        if (unreadNotifications.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var n in unreadNotifications)
            {
                n.ReadAt = now;
            }

            var outboxEvent = new OutboxEvent
            {
                Type = "NotificationsCleared",
                TargetUserId = request.UserId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    userId = request.UserId
                })
            };
            _db.OutboxEvents.Add(outboxEvent);

            await _db.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.Ok(true, "تم مسح جميع التنبيهات بنجاح");
    }
}
