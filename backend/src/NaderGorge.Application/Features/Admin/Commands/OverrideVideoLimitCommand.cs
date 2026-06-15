using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;


using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public class OverrideVideoLimitCommand : IRequest<ApiResponse>
{
    public Guid UserId { get; set; }
    public Guid VideoId { get; set; }
    public int AddedViews { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid AdminId { get; set; }

    public OverrideVideoLimitCommand(Guid userId, Guid videoId, int addedViews, string reason, Guid adminId)
    {
        UserId = userId;
        VideoId = videoId;
        AddedViews = addedViews;
        Reason = reason;
        AdminId = adminId;
    }
}

public class OverrideVideoLimitCommandHandler : IRequestHandler<OverrideVideoLimitCommand, ApiResponse>
{
    private readonly IAppDbContext _context;

    public OverrideVideoLimitCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse> Handle(OverrideVideoLimitCommand request, CancellationToken cancellationToken)
    {
        // Find existing watch event or throw
        var watchEvent = await _context.VideoWatchEvents
            .Include(v => v.LessonVideo)
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.VideoId, cancellationToken);

        if (watchEvent == null)
        {
            throw new KeyNotFoundException("VideoWatchEvent not found");
        }

        int oldLimit = watchEvent.CustomMaxWatchCount ?? watchEvent.LessonVideo.MaxWatchCount;

        // "Adding views" increases the maximum watch count for this student.
        watchEvent.CustomMaxWatchCount = oldLimit + request.AddedViews;
        watchEvent.IsLocked = false;

        var videoOverride = new VideoOverride
        {
            UserId = request.UserId,
            LessonVideoId = request.VideoId,
            OriginalLimit = oldLimit,
            NewLimit = watchEvent.CustomMaxWatchCount.Value,
            AddedViews = request.AddedViews,
            Reason = request.Reason,
            PerformedByUserId = request.AdminId,
            CreatedAt = DateTime.UtcNow
        };
        _context.VideoOverrides.Add(videoOverride);

        // Write to AuditLog
        var audit = new AuditLog
        {
            EntityType = "User",
            EntityId = request.UserId,
            Action = "OVERRIDE_VIEWS",
            PerformedByUserId = request.AdminId,
            OldValues = JsonSerializer.Serialize(new { customMaxWatchCount = oldLimit, action = "increase limit" }),
            NewValues = JsonSerializer.Serialize(new { customMaxWatchCount = watchEvent.CustomMaxWatchCount, addedViews = request.AddedViews, reason = request.Reason })
        };
        _context.AuditLogs.Add(audit);

        var limitChangedEvent = new OutboxEvent
        {
            Type = "VideoWatchLimitChanged",
            TargetUserId = request.UserId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new
            {
                userId = request.UserId,
                videoId = request.VideoId,
                newLimit = watchEvent.CustomMaxWatchCount.Value,
                lessonId = watchEvent.LessonVideo.LessonId
            })
        };
        _context.OutboxEvents.Add(limitChangedEvent);


        // Automatically approve any pending extra watch request for this video/student since their limit is overridden
        var pendingRequests = await _context.ExtraWatchRequests
            .Where(r => r.UserId == request.UserId && r.LessonVideoId == request.VideoId && r.Status == NaderGorge.Domain.Enums.RequestStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var req in pendingRequests)
        {
            req.Status = NaderGorge.Domain.Enums.RequestStatus.Approved;
            req.ResolvedAt = DateTime.UtcNow;
            req.RejectionReason = $"تم زيادة المشاهدات تلقائيًا بواسطة التجاوز: {request.Reason}";

            var outboxEvent = new OutboxEvent
            {
                Type = "ExtraWatchRequestUpdated",
                TargetUserId = req.UserId.ToString(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    lessonId = watchEvent.LessonVideo.LessonId,
                    videoId = req.LessonVideoId,
                    status = "Approved",
                    allowedWatchCount = watchEvent.CustomMaxWatchCount.Value
                })
            };
            _context.OutboxEvents.Add(outboxEvent);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Video limit overridden successfully.");
    }
}

