using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ResetWatchLimitCommand(Guid LessonVideoId, Guid StudentId, Guid AdminId) : IRequest<ApiResponse>;

public class ResetWatchLimitCommandHandler : IRequestHandler<ResetWatchLimitCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public ResetWatchLimitCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(ResetWatchLimitCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);
        if (video == null) return ApiResponse.Fail("Video not found");

        // Delete all watch events for this student + video
        var events = await _db.VideoWatchEvents
            .Where(e => e.LessonVideoId == request.LessonVideoId && e.UserId == request.StudentId)
            .ToListAsync(ct);

        if (events.Count == 0) return ApiResponse.Fail("No watch events found for this student/video combination.");

        _db.VideoWatchEvents.RemoveRange(events);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "ResetWatchLimit",
            EntityType = "LessonVideo",
            EntityId = request.LessonVideoId,
            PerformedByUserId = request.AdminId,
            NewValues = $"Reset {events.Count} watch events for student {request.StudentId} on video {request.LessonVideoId}",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok($"Successfully reset {events.Count} watch events. Student can now re-watch.");
    }
}
