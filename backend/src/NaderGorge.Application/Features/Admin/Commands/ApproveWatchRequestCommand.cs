using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ApproveWatchRequestCommand(Guid RequestId, Guid AdminId) : IRequest<ApiResponse<bool>>;

public class ApproveWatchRequestCommandHandler : IRequestHandler<ApproveWatchRequestCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _context;

    public ApproveWatchRequestCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<bool>> Handle(ApproveWatchRequestCommand request, CancellationToken cancellationToken)
    {
        var req = await _context.ExtraWatchRequests
            .Include(r => r.LessonVideo)
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);

        if (req == null)
            return ApiResponse<bool>.Fail("Request not found", new List<string> { "NOT_FOUND" });

        if (req.Status != RequestStatus.Pending)
            return ApiResponse<bool>.Fail("Request is already resolved", new List<string> { "ALREADY_RESOLVED" });

        req.Status = RequestStatus.Approved;
        req.ResolvedAt = DateTime.UtcNow;

        var watchEvent = await _context.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == req.UserId && v.LessonVideoId == req.LessonVideoId, cancellationToken);

        if (watchEvent != null)
        {
            watchEvent.IsLocked = false;
            // MaxWatchCount might be 0 meaning unlimited, but if it was locked, it has a limit.
            // Increment the custom max limit by 1 to allow 1 more view
            int maxLimit = watchEvent.CustomMaxWatchCount ?? req.LessonVideo.MaxWatchCount;
            if (maxLimit > 0)
            {
                watchEvent.CustomMaxWatchCount = maxLimit + 1;

                var videoOverride = new VideoOverride
                {
                    UserId = req.UserId,
                    LessonVideoId = req.LessonVideoId,
                    OriginalLimit = maxLimit,
                    NewLimit = watchEvent.CustomMaxWatchCount.Value,
                    AddedViews = 1,
                    Reason = "قبول طلب مشاهدة إضافية من الطالب",
                    PerformedByUserId = request.AdminId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.VideoOverrides.Add(videoOverride);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true);
    }
}
