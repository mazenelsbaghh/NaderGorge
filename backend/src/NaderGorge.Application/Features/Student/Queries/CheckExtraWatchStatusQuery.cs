using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;

using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record ExtraWatchStatusDto
{
    public bool CanWatch { get; init; }
    public bool HasPendingRequest { get; init; }
    public bool HasRejectedRequest { get; init; }
    public RequestStatus? RequestStatus { get; init; }
    public string? RejectionReason { get; init; }
}

public record CheckExtraWatchStatusQuery(Guid LessonVideoId, Guid UserId) : IRequest<ApiResponse<ExtraWatchStatusDto>>;

public class CheckExtraWatchStatusQueryHandler : IRequestHandler<CheckExtraWatchStatusQuery, ApiResponse<ExtraWatchStatusDto>>
{
    private readonly IAppDbContext _context;

    public CheckExtraWatchStatusQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ExtraWatchStatusDto>> Handle(CheckExtraWatchStatusQuery request, CancellationToken cancellationToken)
    {
        var watchEvent = await _context.VideoWatchEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.LessonVideoId == request.LessonVideoId && item.UserId == request.UserId,
                cancellationToken);

        // The current lock state is authoritative. A rejected request remains in the
        // audit history, but must not block the student after an admin manually adds views.
        var canWatch = watchEvent is null || !watchEvent.IsLocked;

        var latestRequest = await _context.ExtraWatchRequests
            .AsNoTracking()
            .Where(r => r.LessonVideoId == request.LessonVideoId && r.UserId == request.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (canWatch || latestRequest == null)
        {
            return ApiResponse<ExtraWatchStatusDto>.Ok(new ExtraWatchStatusDto
            {
                CanWatch = canWatch,
                HasPendingRequest = false,
                HasRejectedRequest = false,
                RequestStatus = null
            });
        }

        return ApiResponse<ExtraWatchStatusDto>.Ok(new ExtraWatchStatusDto
        {
            CanWatch = false,
            HasPendingRequest = latestRequest.Status == RequestStatus.Pending,
            HasRejectedRequest = latestRequest.Status == RequestStatus.Rejected,
            RequestStatus = latestRequest.Status,
            RejectionReason = latestRequest.Status == RequestStatus.Rejected ? latestRequest.RejectionReason : null
        });
    }
}
