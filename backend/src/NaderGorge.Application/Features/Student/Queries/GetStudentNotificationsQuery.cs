using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetStudentNotificationsQuery(Guid UserId) : IRequest<ApiResponse<List<StudentNotificationDto>>>;

public record StudentNotificationDto(
    Guid Id,
    string Title,
    string Body,
    bool IsRead,
    DateTime CreatedAt,
    string? ActionUrl = null
);

public class GetStudentNotificationsQueryHandler : IRequestHandler<GetStudentNotificationsQuery, ApiResponse<List<StudentNotificationDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public GetStudentNotificationsQueryHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<List<StudentNotificationDto>>> Handle(GetStudentNotificationsQuery request, CancellationToken ct)
    {
        var rows = await _db.NotificationEvents
            .AsNoTracking()
            .Where(n => n.UserId == request.UserId && n.ChannelType == NotificationChannelType.InApp)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        if (_academicScope != null)
        {
            var eligibleRows = new List<NotificationEvent>(rows.Count);
            foreach (var notification in rows)
            {
                if (await IsNotificationEligibleAsync(notification, request.UserId, ct))
                    eligibleRows.Add(notification);
            }
            rows = eligibleRows;
        }

        var links = await LessonYouTubeLinks.ResolveAsync(_db, rows.SelectMany(n => new[] { n.Title, n.Body, n.ActionUrl }), ct);
        var notifications = rows.Select(n =>
        {
            var lessonId = n.AcademicScopeOwnerType == StudentFacingScopeOwnerType.Lesson ? n.AcademicScopeOwnerId : null;
            return new StudentNotificationDto(n.Id, links.Rewrite(n.Title, lessonId), links.Rewrite(n.Body, lessonId),
                n.ReadAt != null, n.CreatedAt, links.FindLessonPath(n.ActionUrl, lessonId) ?? n.ActionUrl);
        }).ToList();

        return ApiResponse<List<StudentNotificationDto>>.Ok(notifications);
    }

    private async Task<bool> IsNotificationEligibleAsync(NotificationEvent notification, Guid userId, CancellationToken ct)
    {
        if (notification.AcademicScopeOwnerType == null || notification.AcademicScopeOwnerId == null)
            return true;

        return await _academicScope!.IsOwnerEligibleForStudentAsync(
            notification.AcademicScopeOwnerType.Value,
            notification.AcademicScopeOwnerId.Value,
            userId,
            ct);
    }
}
