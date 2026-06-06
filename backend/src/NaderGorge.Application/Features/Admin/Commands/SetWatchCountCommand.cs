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
