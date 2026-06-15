using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ToggleVideoActiveCommand(Guid VideoId, Guid CurrentUserId) : IRequest<ApiResponse<bool>>;

public class ToggleVideoActiveCommandHandler : IRequestHandler<ToggleVideoActiveCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public ToggleVideoActiveCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<bool>> Handle(ToggleVideoActiveCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
        if (video == null) return ApiResponse<bool>.Fail("Video not found.");

        var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId, video.LessonId, ct);
        if (!canAccess) return ApiResponse<bool>.Fail("Unauthorized access to this video.");

        video.IsActive = !video.IsActive;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(video.IsActive);
    }
}
