using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record TrackWatchProgressCommand(
    Guid LessonVideoId,
    Guid UserId,
    Guid SessionId,
    long ProgressSequence,
    double SecondsWatched,
    int TotalDurationSeconds
) : IRequest<ApiResponse<WatchProgressDto>>;

public record WatchProgressDto(
    int CurrentCount,
    int MaxCount,
    bool IsLocked,
    bool ViewRegistered,
    int TotalTrackedSeconds,
    int ThresholdSeconds,
    DateTime SessionExpiresAt,
    bool Duplicate
);

public class TrackWatchProgressCommandHandler : IRequestHandler<TrackWatchProgressCommand, ApiResponse<WatchProgressDto>>
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(5);

    private readonly IAppDbContext _db;
    private readonly ICachedPlatformSettingsReader _cachedPlatformSettingsReader;

    public TrackWatchProgressCommandHandler(IAppDbContext db, ICachedPlatformSettingsReader cachedPlatformSettingsReader)
    {
        _db = db;
        _cachedPlatformSettingsReader = cachedPlatformSettingsReader;
    }

    public async Task<ApiResponse<WatchProgressDto>> Handle(TrackWatchProgressCommand request, CancellationToken ct)
    {
        if (request.TotalDurationSeconds <= 0)
            return Fail("Duration required", "DURATION_REQUIRED");

        if (request.ProgressSequence <= 0)
            return Fail("Progress sequence required", "PROGRESS_SEQUENCE_REQUIRED");

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var session = await _db.VideoPlaybackSessions.FirstOrDefaultAsync(
            s => s.Id == request.SessionId
                 && s.UserId == request.UserId
                 && s.LessonVideoId == request.LessonVideoId,
            ct);

        if (session == null)
            return Fail("Invalid playback session", "SESSION_INVALID");

        var now = DateTime.UtcNow;
        var sessionError = await GetSessionErrorAsync(session, request, now, ct);
        if (sessionError != null)
            return sessionError;

        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);
        if (video == null)
            return Fail("Video not found", "VIDEO_NOT_FOUND");

        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var thresholdSeconds = VideoWatchProgressCalculator.ResolveThresholdSeconds(
            request.TotalDurationSeconds,
            settings.VideoWatchThresholdPercentage);
        var maxLimit = watchEvent?.CustomMaxWatchCount ?? video.MaxWatchCount;

        if (request.ProgressSequence <= session.LastProgressSequence)
        {
            await transaction.CommitAsync(ct);
            return ApiResponse<WatchProgressDto>.Ok(CreateDto(new WatchProgressSnapshot(
                watchEvent,
                maxLimit,
                thresholdSeconds,
                session.ExpiresAt,
                ViewRegistered: false,
                Duplicate: true)));
        }

        var isNewWatchEvent = watchEvent == null;
        watchEvent ??= CreateWatchEvent(request, now);

        if (watchEvent.TimeWatchedInSeconds < 0)
            watchEvent.TimeWatchedInSeconds = watchEvent.WatchCount * thresholdSeconds;

        maxLimit = watchEvent.CustomMaxWatchCount ?? video.MaxWatchCount;
        var isStaffOrTeacher = await _db.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.UserId == request.UserId && ur.Role.Type != RoleType.Student, ct);

        var isLocked = !isStaffOrTeacher && maxLimit > 0 && watchEvent.WatchCount >= maxLimit;
        if (isLocked)
        {
            watchEvent.WatchCount = Math.Min(watchEvent.WatchCount, maxLimit);
            watchEvent.IsLocked = true;
        }
        else if (watchEvent.IsLocked)
        {
            watchEvent.IsLocked = false;
        }

        var viewRegistered = ApplySessionProgress(new SessionProgressContext(
            request,
            session,
            watchEvent,
            thresholdSeconds,
            maxLimit,
            now,
            isNewWatchEvent,
            isLocked));

        RenewSession(session, request.ProgressSequence, now);
        watchEvent.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ApiResponse<WatchProgressDto>.Ok(CreateDto(new WatchProgressSnapshot(
            watchEvent,
            maxLimit,
            thresholdSeconds,
            session.ExpiresAt,
            viewRegistered,
            Duplicate: false)));
    }

    private static ApiResponse<WatchProgressDto> Fail(string message, string error) =>
        ApiResponse<WatchProgressDto>.Fail(message, new List<string> { error });

    private async Task<ApiResponse<WatchProgressDto>?> GetSessionErrorAsync(
        VideoPlaybackSession session,
        TrackWatchProgressCommand request,
        DateTime now,
        CancellationToken ct)
    {
        if (session.IsSuperseded)
            return Fail("Playback session was superseded", "SESSION_SUPERSEDED");
        if (session.ExpiresAt <= now)
            return Fail("Playback session expired", "SESSION_EXPIRED");

        var hasNewerSession = await _db.VideoPlaybackSessions.AnyAsync(
            candidate => candidate.UserId == request.UserId
                         && candidate.LessonVideoId == request.LessonVideoId
                         && candidate.Id != session.Id
                         && !candidate.IsSuperseded
                         && candidate.CreatedAt > session.CreatedAt,
            ct);
        return hasNewerSession
            ? Fail("Playback session was superseded", "SESSION_SUPERSEDED")
            : null;
    }

    private VideoWatchEvent CreateWatchEvent(TrackWatchProgressCommand request, DateTime now)
    {
        var watchEvent = new VideoWatchEvent
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            LessonVideoId = request.LessonVideoId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.VideoWatchEvents.Add(watchEvent);
        return watchEvent;
    }

    private static bool ApplySessionProgress(SessionProgressContext context)
    {
        if (context.IsLocked || context.Session.HasRegisteredView)
            return false;

        var acceptedSeconds = VideoWatchProgressCalculator.ResolveAcceptedSeconds(
            context.Request.SecondsWatched,
            context.Now,
            context.WatchEvent,
            context.IsNewWatchEvent);
        var boundedSeconds = VideoWatchProgressCalculator.CapAtNextViewBoundary(
            context.WatchEvent,
            acceptedSeconds,
            context.ThresholdSeconds);
        var progress = VideoWatchProgressCalculator.ApplyProgress(
            context.WatchEvent,
            boundedSeconds,
            context.ThresholdSeconds,
            context.MaxLimit);
        context.Session.HasRegisteredView = progress.ViewRegistered;
        return progress.ViewRegistered;
    }

    private static void RenewSession(VideoPlaybackSession session, long progressSequence, DateTime now)
    {
        session.LastProgressSequence = progressSequence;
        session.LastProgressAt = now;
        session.ExpiresAt = now.Add(SessionLifetime);
        session.UpdatedAt = now;
    }

    private static WatchProgressDto CreateDto(WatchProgressSnapshot snapshot)
    {
        var watchCount = snapshot.WatchEvent?.WatchCount ?? 0;
        var isLocked = snapshot.MaxLimit > 0 && watchCount >= snapshot.MaxLimit;
        return new WatchProgressDto(
            snapshot.MaxLimit > 0 ? Math.Min(watchCount, snapshot.MaxLimit) : watchCount,
            snapshot.MaxLimit,
            isLocked,
            snapshot.ViewRegistered,
            Math.Max(0, snapshot.WatchEvent?.TimeWatchedInSeconds ?? 0),
            snapshot.ThresholdSeconds,
            snapshot.SessionExpiresAt,
            snapshot.Duplicate);
    }

    private sealed record WatchProgressSnapshot(
        VideoWatchEvent? WatchEvent,
        int MaxLimit,
        int ThresholdSeconds,
        DateTime SessionExpiresAt,
        bool ViewRegistered,
        bool Duplicate);

    private sealed record SessionProgressContext(
        TrackWatchProgressCommand Request,
        VideoPlaybackSession Session,
        VideoWatchEvent WatchEvent,
        int ThresholdSeconds,
        int MaxLimit,
        DateTime Now,
        bool IsNewWatchEvent,
        bool IsLocked);
}
