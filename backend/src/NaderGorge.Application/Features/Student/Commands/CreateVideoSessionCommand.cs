using MediatR;
using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
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
        if (!VideoProviders.IsSupported(video.Provider))
        {
            return ApiResponse<VideoSessionDto>.Fail("Invalid video provider", new List<string> { "INVALID_PROVIDER" });
        }

        // 1. Verify access to the package
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, video.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<VideoSessionDto>.Fail("You do not have access to this video", new List<string> { "ACCESS_DENIED" });

        // 1b. Check if the current video has an unpassed mandatory exam
        var videoExams = await _db.Exams
            .Where(e => e.IsMandatory && (
                e.LessonVideoId == video.Id ||
                (video.ExamId == e.Id)
            ))
            .Select(e => e.Id)
            .ToListAsync(ct);

        if (videoExams.Any())
        {
            var passedVideoExamIds = await _db.StudentExamAttempts
                .Where(a => a.UserId == request.UserId && videoExams.Contains(a.ExamId) && a.IsPassed)
                .Select(a => a.ExamId)
                .ToListAsync(ct);

            if (passedVideoExamIds.Count < videoExams.Count)
            {
                return ApiResponse<VideoSessionDto>.Fail("This video is locked by a mandatory exam.", new List<string> { "EXAM_LOCKED" });
            }
        }

        // 2. Check watch limits
        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        var maxCount = watchEvent?.CustomMaxWatchCount ?? video.MaxWatchCount;
        int currentCount = watchEvent == null
            ? 0
            : maxCount > 0 ? Math.Min(watchEvent.WatchCount, maxCount) : watchEvent.WatchCount;
        bool isLocked = maxCount > 0 && currentCount >= maxCount;

        var isStaffOrTeacher = await _db.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.UserId == request.UserId && ur.Role.Type != RoleType.Student, ct);

        if (isLocked && !isStaffOrTeacher)
        {
            // Also ensure the flag is persisted so future checks are fast
            if (watchEvent != null && !watchEvent.IsLocked)
            {
                watchEvent.IsLocked = true;
                await _db.SaveChangesAsync(ct);
            }

            // Return real watch info so the player can display accurate counts (e.g. 5 من أصل 5, not a hardcoded fallback)
            var lockedDto = new VideoSessionDto(
                Guid.Empty,
                DateTime.MinValue,
                video.Provider,
                new WatchInfoDto(
                    currentCount,
                    maxCount,
                    IsLocked: true,
                    TotalTrackedSeconds: Math.Max(0, watchEvent?.TimeWatchedInSeconds ?? 0)),
                video.Title,
                30);
            return ApiResponse<VideoSessionDto>.Fail("Watch limit reached for this video", new List<string> { "WATCH_LIMIT_REACHED" }, lockedDto);
        }
        else
        {
            // Self-repair out-of-sync DB flag
            if (watchEvent != null && watchEvent.IsLocked)
            {
                watchEvent.IsLocked = false;
                await _db.SaveChangesAsync(ct);
            }
        }



        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTime.UtcNow;
        var priorActiveSessions = await _db.VideoPlaybackSessions
            .Where(s => s.UserId == request.UserId
                        && s.LessonVideoId == request.LessonVideoId
                        && !s.IsSuperseded
                        && s.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var priorSession in priorActiveSessions)
        {
            priorSession.IsSuperseded = true;
            priorSession.UpdatedAt = now;
        }

        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
        var session = new VideoPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            LessonVideoId = request.LessonVideoId,
            EncryptionKey = _encryption.GenerateSessionKey(),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(5),
            IsConsumed = false,
            HasRegisteredView = false,
            LastProgressSequence = 0,
            IsSuperseded = false,
            IpAddress = request.IpAddress
        };

        string studentName = user?.FullName ?? "Unknown";
        string studentPhone = user?.PhoneNumber ?? "Unknown";
        session.SessionToken = _encryption.EncryptVideoInfo(
            video.Provider,
            video.ProviderVideoId,
            session.EncryptionKey,
            studentName,
            studentPhone);

        _db.VideoPlaybackSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

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
            new WatchInfoDto(currentCount, maxCount, isLocked, Math.Max(0, watchEvent?.TimeWatchedInSeconds ?? 0)),
            video.Title,
            thresholdPercentage
        );

        return ApiResponse<VideoSessionDto>.Ok(dto);
    }
}
