using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ApproveWatchRequestCommand(Guid RequestId, Guid AdminId, string? Reason = null, int AddedViews = 1) : IRequest<ApiResponse<bool>>;

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

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "تمت الموافقة بواسطة الإدارة"
            : request.Reason.Trim();
        if (reason.Length > 1000)
            return ApiResponse<bool>.Fail("Approval reason is too long", ["REASON_TOO_LONG"]);

        var watchEvent = await _context.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == req.UserId && v.LessonVideoId == req.LessonVideoId, cancellationToken);
        var currentLimit = watchEvent?.CustomMaxWatchCount ?? req.LessonVideo.MaxWatchCount;
        var addedViews = request.AddedViews > 0 ? request.AddedViews : 1;
        if (watchEvent is not null && currentLimit > 0 && currentLimit > int.MaxValue - addedViews)
            return ApiResponse<bool>.Fail("Watch limit would overflow", ["WATCH_LIMIT_OVERFLOW"]);

        req.Status = RequestStatus.Approved;
        req.ResolvedAt = DateTime.UtcNow;
        req.RejectionReason = reason;

        if (watchEvent != null)
        {
            watchEvent.IsLocked = false;
            // MaxWatchCount might be 0 meaning unlimited, but if it was locked, it has a limit.
            // Increment the custom max limit by AddedViews
            if (currentLimit > 0)
            {
                watchEvent.CustomMaxWatchCount = currentLimit + addedViews;
                // Force a progress reset on next play session so they start the new view with a clean threshold baseline
                watchEvent.TimeWatchedInSeconds = -1;

                var videoOverride = new VideoOverride
                {
                    UserId = req.UserId,
                    LessonVideoId = req.LessonVideoId,
                    OriginalLimit = currentLimit,
                    NewLimit = watchEvent.CustomMaxWatchCount.Value,
                    AddedViews = addedViews,
                    Reason = $"قبول/تعديل طلب مشاهدة إضافية: {reason}",
                    PerformedByUserId = request.AdminId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.VideoOverrides.Add(videoOverride);
            }
        }

        var outboxEvent = new OutboxEvent
        {
            Type = "ExtraWatchRequestUpdated",
            TargetUserId = req.UserId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                requestId = req.Id,
                lessonId = req.LessonVideo.LessonId,
                videoId = req.LessonVideoId,
                status = "Approved",
                allowedWatchCount = watchEvent?.CustomMaxWatchCount ?? req.LessonVideo.MaxWatchCount,
                reason
            })
        };
        _context.OutboxEvents.Add(outboxEvent);

        _context.OutboxEvents.Add(new OutboxEvent
        {
            Type = "ExtraWatchRequestUpdated",
            TargetGroup = "Role_Staff",
            PayloadJson = outboxEvent.PayloadJson
        });

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true);
    }
}
