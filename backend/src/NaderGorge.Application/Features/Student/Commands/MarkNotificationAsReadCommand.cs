using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Student.Commands;

public record MarkNotificationAsReadCommand(Guid NotificationId, Guid UserId) : IRequest<ApiResponse<bool>>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public MarkNotificationAsReadCommandHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<bool>> Handle(MarkNotificationAsReadCommand request, CancellationToken ct)
    {
        var notification = await _db.NotificationEvents
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == request.UserId, ct);

        if (notification == null)
        {
            return ApiResponse<bool>.Fail("التنبيه غير موجود");
        }

        if (_academicScope != null &&
            notification.AcademicScopeOwnerType.HasValue &&
            notification.AcademicScopeOwnerId.HasValue &&
            !await _academicScope.IsOwnerEligibleForStudentAsync(
                notification.AcademicScopeOwnerType.Value,
                notification.AcademicScopeOwnerId.Value,
                request.UserId,
                ct))
        {
            return ApiResponse<bool>.Fail("التنبيه غير متاح لنطاقك الدراسي الحالي.", new List<string> { "ACADEMIC_SCOPE_DENIED" });
        }

        notification.ReadAt = DateTime.UtcNow;

        var notificationReadEvent = new OutboxEvent
        {
            Type = "NotificationRead",
            TargetUserId = request.UserId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                notificationId = request.NotificationId,
                userId = request.UserId
            })
        };
        _db.OutboxEvents.Add(notificationReadEvent);

        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }
}
