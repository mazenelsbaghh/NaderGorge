using System.Linq;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record SetWatchCountCommand(Guid LessonVideoId, Guid StudentId, int NewWatchCount, Guid AdminId) : IRequest<ApiResponse>;

public class SetWatchCountCommandHandler : IRequestHandler<SetWatchCountCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public SetWatchCountCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(SetWatchCountCommand request, CancellationToken ct)
    {
        if (request.NewWatchCount < 0)
            return ApiResponse.Fail("Watch count cannot be negative.");

        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(e => e.LessonVideoId == request.LessonVideoId && e.UserId == request.StudentId, ct);

        if (watchEvent == null)
            return ApiResponse.Fail("No watch record found for this student/video combination.");

        var oldCount = watchEvent.WatchCount;
        watchEvent.WatchCount = request.NewWatchCount;

        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);
        if (video != null)
        {
            int maxLimit = watchEvent.CustomMaxWatchCount ?? video.MaxWatchCount;
            if (maxLimit > 0)
            {
                watchEvent.IsLocked = request.NewWatchCount >= maxLimit;
            }

            if (!watchEvent.IsLocked)
            {
                // Automatically approve any pending extra watch request for this video/student since they are now unlocked
                var pendingRequests = await _db.ExtraWatchRequests
                    .Where(r => r.UserId == request.StudentId && r.LessonVideoId == request.LessonVideoId && r.Status == NaderGorge.Domain.Enums.RequestStatus.Pending)
                    .ToListAsync(ct);

                foreach (var req in pendingRequests)
                {
                    req.Status = NaderGorge.Domain.Enums.RequestStatus.Approved;
                    req.ResolvedAt = DateTime.UtcNow;
                    req.RejectionReason = "تم فتح الفيديو يدويًا عن طريق تعديل عدد المشاهدات";

                    var outboxEvent = new OutboxEvent
                    {
                        Type = "ExtraWatchRequestUpdated",
                        TargetUserId = req.UserId.ToString(),
                        PayloadJson = JsonSerializer.Serialize(new
                        {
                            lessonId = video.LessonId,
                            videoId = req.LessonVideoId,
                            status = "Approved",
                            allowedWatchCount = maxLimit
                        })
                    };
                    _db.OutboxEvents.Add(outboxEvent);
                }
            }
        }


        _db.AuditLogs.Add(new AuditLog
        {
            Action = "SetWatchCount",
            EntityType = "VideoWatchEvent",
            EntityId = request.LessonVideoId,
            PerformedByUserId = request.AdminId,
            NewValues = $"Changed watch count from {oldCount} to {request.NewWatchCount} for student {request.StudentId}",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok($"Watch count updated from {oldCount} to {request.NewWatchCount}.");
    }
}
