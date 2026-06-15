using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace NaderGorge.Application.Features.Admin.Queries;

public record AdminWatchRequestDto(
    Guid Id,
    Guid UserId,
    string StudentName,
    string StudentPhone,
    Guid LessonVideoId,
    string VideoTitle,
    int Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? Reason,
    int CurrentWatchCount,
    int MaxWatchCount,
    bool ReachedLimit
);

public record GetWatchRequestsQuery() : IRequest<ApiResponse<List<AdminWatchRequestDto>>>;

public class GetWatchRequestsQueryHandler : IRequestHandler<GetWatchRequestsQuery, ApiResponse<List<AdminWatchRequestDto>>>
{
    private readonly IAppDbContext _context;

    public GetWatchRequestsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<AdminWatchRequestDto>>> Handle(GetWatchRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await _context.ExtraWatchRequests
            .Include(r => r.User)
            .Include(r => r.LessonVideo)
            .OrderBy(r => r.Status == NaderGorge.Domain.Enums.RequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.Id,
                r.UserId,
                StudentName = r.User != null ? r.User.FullName : string.Empty,
                StudentPhone = r.User != null ? r.User.PhoneNumber : string.Empty,
                r.LessonVideoId,
                VideoTitle = r.LessonVideo != null ? r.LessonVideo.Title : string.Empty,
                Status = (int)r.Status,
                r.CreatedAt,
                r.ResolvedAt,
                Reason = r.RejectionReason,
                WatchEvent = _context.VideoWatchEvents
                    .Where(w => w.UserId == r.UserId && w.LessonVideoId == r.LessonVideoId)
                    .Select(w => new { w.WatchCount, MaxLimit = w.CustomMaxWatchCount })
                    .FirstOrDefault(),
                VideoMaxLimit = r.LessonVideo != null ? r.LessonVideo.MaxWatchCount : 0
            })
            .ToListAsync(cancellationToken);

        var dtos = requests.Select(r => {
            int currentCount = r.WatchEvent?.WatchCount ?? 0;
            int maxCount = r.WatchEvent?.MaxLimit ?? r.VideoMaxLimit;
            bool reachedLimit = maxCount > 0 && currentCount >= maxCount;
            return new AdminWatchRequestDto(
                r.Id,
                r.UserId,
                r.StudentName,
                r.StudentPhone,
                r.LessonVideoId,
                r.VideoTitle,
                r.Status,
                r.CreatedAt,
                r.ResolvedAt,
                r.Reason,
                currentCount,
                maxCount,
                reachedLimit
            );
        }).ToList();

        return ApiResponse<List<AdminWatchRequestDto>>.Ok(dtos);
    }
}

