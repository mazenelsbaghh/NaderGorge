using System;
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

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Video limit overridden successfully.");
    }
}
