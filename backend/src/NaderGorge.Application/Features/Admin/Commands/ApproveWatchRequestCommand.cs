using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ApproveWatchRequestCommand(Guid RequestId) : IRequest<ApiResponse<bool>>;

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
            // Reset to max-1 to allow 1 more view
            int maxCount = req.LessonVideo.MaxWatchCount;
            if (maxCount > 0)
            {
                watchEvent.WatchCount = Math.Max(0, maxCount - 1);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true);
    }
}
