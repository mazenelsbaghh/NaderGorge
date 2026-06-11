using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record RejectWatchRequestCommand(Guid RequestId, string Reason) : IRequest<ApiResponse<bool>>;

public class RejectWatchRequestCommandHandler : IRequestHandler<RejectWatchRequestCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _context;

    public RejectWatchRequestCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<bool>> Handle(RejectWatchRequestCommand request, CancellationToken cancellationToken)
    {
        var req = await _context.ExtraWatchRequests
            .Include(r => r.LessonVideo)
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);

        if (req == null)
            return ApiResponse<bool>.Fail("Request not found", new List<string> { "NOT_FOUND" });

        if (req.Status != RequestStatus.Pending)
            return ApiResponse<bool>.Fail("Request is already resolved", new List<string> { "ALREADY_RESOLVED" });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<bool>.Fail("Rejection reason is required.", new List<string> { "REJECTION_REASON_REQUIRED" });

        var reason = request.Reason.Trim();

        req.Status = RequestStatus.Rejected;
        req.ResolvedAt = DateTime.UtcNow;
        req.RejectionReason = reason;

        var outboxEvent = new OutboxEvent
        {
            Type = "ExtraWatchRequestUpdated",
            TargetUserId = req.UserId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = req.LessonVideo.LessonId,
                videoId = req.LessonVideoId,
                status = "Rejected",
                allowedWatchCount = 0
            })
        };
        _context.OutboxEvents.Add(outboxEvent);

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true);
    }
}
