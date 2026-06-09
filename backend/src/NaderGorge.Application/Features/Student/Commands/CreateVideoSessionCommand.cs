using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record CreateVideoSessionCommand(Guid LessonVideoId, Guid UserId, string? IpAddress = null) : IRequest<ApiResponse<VideoSessionDto>>;

public record VideoSessionDto(
    Guid SessionId,
    DateTime ExpiresAt,
    string Provider,
    WatchInfoDto WatchInfo,
    string VideoTitle,
    int ThresholdPercentage
);

public record WatchInfoDto(int CurrentCount, int MaxCount, bool IsLocked, int TotalTrackedSeconds);

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

        // Validate provider
        if (!string.Equals(video.Provider, "youtube", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(video.Provider, "vk", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<VideoSessionDto>.Fail("Invalid video provider", new List<string> { "INVALID_PROVIDER" });
        }

        // 1. Verify access to the package
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, video.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<VideoSessionDto>.Fail("You do not have access to this video", new List<string> { "ACCESS_DENIED" });

        // 2. Check watch limits
        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        var maxCount = watchEvent?.CustomMaxWatchCount ?? video.MaxWatchCount;
        int currentCount = watchEvent == null
            ? 0
            : maxCount > 0 ? Math.Min(watchEvent.WatchCount, maxCount) : watchEvent.WatchCount;
        bool isLocked = watchEvent?.IsLocked ?? false;

        var isAdminOrTeacher = await _db.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.UserId == request.UserId && (ur.Role.Name == "Admin" || ur.Role.Name == "Teacher"), ct);

        if (isLocked && !isAdminOrTeacher)
        {
            // Return real watch info so the player can display accurate counts (e.g. 5 من أصل 5, not a hardcoded fallback)
            var lockedDto = new VideoSessionDto(
                Guid.Empty,
                DateTime.MinValue,
                video.Provider,
                new WatchInfoDto(
                    currentCount,
                    maxCount,
                    IsLocked: true,
                    TotalTrackedSeconds: watchEvent?.TimeWatchedInSeconds ?? 0),
                video.Title,
                30);
            return ApiResponse<VideoSessionDto>.Fail("Watch limit reached for this video", new List<string> { "WATCH_LIMIT_REACHED" }, lockedDto);
        }


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
            var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);

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
            string studentName = user?.FullName ?? "Unknown";
            string studentPhone = user?.PhoneNumber ?? "Unknown";
            session.SessionToken = _encryption.EncryptVideoInfo(video.Provider, video.ProviderVideoId, session.EncryptionKey, studentName, studentPhone);

            _db.VideoPlaybackSessions.Add(session);
            await _db.SaveChangesAsync(ct);
        }

        // 4. Fetch the global threshold percentage (default 30%)
        var thresholdSetting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == "VideoWatchThresholdPercentage", ct);
        int thresholdPercentage = 30; // default
        if (thresholdSetting != null && int.TryParse(thresholdSetting.Value, out int parsed))
        {
            thresholdPercentage = parsed;
        }

        var dto = new VideoSessionDto(
            session.Id,
            session.ExpiresAt,
            video.Provider,
            new WatchInfoDto(currentCount, maxCount, isLocked, watchEvent?.TimeWatchedInSeconds ?? 0),
            video.Title,
            thresholdPercentage
        );

        return ApiResponse<VideoSessionDto>.Ok(dto);
    }
}
