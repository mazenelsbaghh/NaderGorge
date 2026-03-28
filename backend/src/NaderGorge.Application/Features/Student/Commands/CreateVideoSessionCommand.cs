using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record CreateVideoSessionCommand(Guid LessonVideoId, Guid UserId, string? IpAddress = null) : IRequest<ApiResponse<VideoSessionDto>>;

public record VideoSessionDto(
    Guid SessionId,
    string Token,
    string Key,
    DateTime ExpiresAt,
    string Provider,
    WatchInfoDto WatchInfo,
    string VideoTitle
);

public record WatchInfoDto(int CurrentCount, int MaxCount, bool IsLocked);

public class CreateVideoSessionCommandHandler : IRequestHandler<CreateVideoSessionCommand, ApiResponse<VideoSessionDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;
    private readonly IVideoEncryptionService _encryption;

    public CreateVideoSessionCommandHandler(IAppDbContext db, IAccessCheckService access, IVideoEncryptionService encryption)
    {
        _db = db;
        _access = access;
        _encryption = encryption;
    }

    public async Task<ApiResponse<VideoSessionDto>> Handle(CreateVideoSessionCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos
            .Include(v => v.Lesson)
            .FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);

        if (video == null)
            return ApiResponse<VideoSessionDto>.Fail("Video not found", new List<string> { "VIDEO_NOT_FOUND" });

        // 1. Verify access to the package
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, video.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<VideoSessionDto>.Fail("You do not have access to this video", new List<string> { "ACCESS_DENIED" });

        // 2. Check watch limits
        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        int currentCount = watchEvent?.WatchCount ?? 0;
        bool isLocked = watchEvent?.IsLocked ?? false;

        if (isLocked)
            return ApiResponse<VideoSessionDto>.Fail("Watch limit reached for this video", new List<string> { "WATCH_LIMIT_REACHED" });

        // 3. Prevent duplicate active sessions (optional, but good for security)
        var activeSession = await _db.VideoPlaybackSessions
            .Where(s => s.UserId == request.UserId && s.LessonVideoId == request.LessonVideoId && !s.IsConsumed && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        VideoPlaybackSession session;

        if (activeSession != null)
        {
            // Reuse active session to prevent spam
            session = activeSession;
        }
        else
        {
            // Create new session
            session = new VideoPlaybackSession
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                LessonVideoId = request.LessonVideoId,
                EncryptionKey = _encryption.GenerateSessionKey(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsConsumed = false,
                IpAddress = request.IpAddress
            };
            
            // Generate the encrypted token
            session.SessionToken = _encryption.EncryptVideoInfo(video.Provider, video.ProviderVideoId, session.EncryptionKey);

            _db.VideoPlaybackSessions.Add(session);
            await _db.SaveChangesAsync(ct);
        }

        var dto = new VideoSessionDto(
            session.Id,
            session.SessionToken,
            session.EncryptionKey,
            session.ExpiresAt,
            video.Provider,
            new WatchInfoDto(currentCount, video.MaxWatchCount, isLocked),
            video.Title
        );

        return ApiResponse<VideoSessionDto>.Ok(dto);
    }
}
