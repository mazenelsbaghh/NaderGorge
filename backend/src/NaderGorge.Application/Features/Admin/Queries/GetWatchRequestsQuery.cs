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
    string? Reason
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
            .Select(r => new AdminWatchRequestDto(
                r.Id,
                r.UserId,
                r.User != null ? r.User.FullName : string.Empty,
                r.User != null ? r.User.PhoneNumber : string.Empty,
                r.LessonVideoId,
                r.LessonVideo != null ? r.LessonVideo.Title : string.Empty,
                (int)r.Status,
                r.CreatedAt,
                r.ResolvedAt,
                r.RejectionReason
            )).ToListAsync(cancellationToken);

        return ApiResponse<List<AdminWatchRequestDto>>.Ok(requests);
    }
}
