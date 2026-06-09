using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities.Notifications;
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
    DateTime CreatedAt
);

public class GetStudentNotificationsQueryHandler : IRequestHandler<GetStudentNotificationsQuery, ApiResponse<List<StudentNotificationDto>>>
{
    private readonly IAppDbContext _db;

    public GetStudentNotificationsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<StudentNotificationDto>>> Handle(GetStudentNotificationsQuery request, CancellationToken ct)
    {
        var notifications = await _db.NotificationEvents
            .AsNoTracking()
            .Where(n => n.UserId == request.UserId && n.ChannelType == NotificationChannelType.InApp)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new StudentNotificationDto(
                n.Id,
                n.Title,
                n.Body,
                n.ReadAt != null,
                n.CreatedAt
            ))
            .ToListAsync(ct);

        return ApiResponse<List<StudentNotificationDto>>.Ok(notifications);
    }
}
