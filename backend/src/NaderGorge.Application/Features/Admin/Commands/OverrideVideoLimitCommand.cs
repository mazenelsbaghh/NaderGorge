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
        if (request.AddedViews <= 0)
            return ApiResponse.Fail("Added views must be greater than zero.");

        var video = await _context.LessonVideos
            .FirstOrDefaultAsync(video => video.Id == request.VideoId, cancellationToken);
        if (video == null) return ApiResponse.Fail("Video not found.");

        var watchEvent = await _context.VideoWatchEvents
            .FirstOrDefaultAsync(item => item.UserId == request.UserId && item.LessonVideoId == request.VideoId, cancellationToken);

        int oldLimit = watchEvent?.CustomMaxWatchCount ?? video.MaxWatchCount;
        if (oldLimit == 0)
            return ApiResponse.Fail("This video already has unlimited views.");

        // Support can add views before the student opens the video for the first time.
        // In that case there is no watch record yet, so create the minimal record first.
        if (watchEvent == null)
        {
            watchEvent = new VideoWatchEvent
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                LessonVideoId = request.VideoId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.VideoWatchEvents.Add(watchEvent);
        }

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
                lessonId = video.LessonId
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
                    lessonId = video.LessonId,
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
