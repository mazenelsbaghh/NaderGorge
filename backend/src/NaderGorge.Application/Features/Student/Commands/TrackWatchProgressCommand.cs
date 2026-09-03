using System.Data;
using System.Collections.Generic;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student;
using NaderGorge.Application.Interfaces;
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
    double PlaybackRate,
    int TotalDurationSeconds,
    IReadOnlyList<WatchProgressSegment>? ProgressSegments = null
) : IRequest<ApiResponse<WatchProgressDto>>;

public record WatchProgressSegment(
    long ProgressSequence,
    double SecondsWatched,
    double PlaybackRate);

public record WatchProgressDto(
    int CurrentCount,
    int MaxCount,
    bool IsLocked,
    bool ViewRegistered,
    bool SessionHasRegisteredView,
    int TotalTrackedSeconds,
    int ThresholdSeconds,
    DateTime SessionExpiresAt,
    bool Duplicate
);

public class TrackWatchProgressCommandHandler : IRequestHandler<TrackWatchProgressCommand, ApiResponse<WatchProgressDto>>
{
    private const int MaxProgressSegmentsPerRequest = 30;
    private const int MaxSecondsPerProgressSegment = 30;
    private readonly IAppDbContext _db;
    private readonly ICachedPlatformSettingsReader _cachedPlatformSettingsReader;
    private readonly IVideoPlaybackConcurrency? _playbackConcurrency;

    public TrackWatchProgressCommandHandler(
        IAppDbContext db,
        ICachedPlatformSettingsReader cachedPlatformSettingsReader,
        IVideoPlaybackConcurrency? playbackConcurrency = null)
    {
        _db = db;
        _cachedPlatformSettingsReader = cachedPlatformSettingsReader;
        _playbackConcurrency = playbackConcurrency;
    }

    public async Task<ApiResponse<WatchProgressDto>> Handle(TrackWatchProgressCommand request, CancellationToken ct)
    {
        var batchValidation = ValidateProgressBatch(request);
        if (!batchValidation.IsValid)
            return Fail(batchValidation.ErrorMessage!, batchValidation.ErrorCode!);

        var progressBatch = batchValidation.Batch!;

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (_playbackConcurrency is not null)
            await _playbackConcurrency.AcquireAsync(request.UserId, request.LessonVideoId, ct);

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

        var trackingPolicy = await ResolveTrackingPolicyAsync(session, video, request, ct);
        if (trackingPolicy is null)
            return Fail("Duration required", "DURATION_REQUIRED");

        var effectiveDurationSeconds = trackingPolicy.DurationSeconds;
        var thresholdSeconds = trackingPolicy.ThresholdSeconds;

        var watchEvent = await _db.VideoWatchEvents
            .FirstOrDefaultAsync(v => v.UserId == request.UserId && v.LessonVideoId == request.LessonVideoId, ct);

        var maxLimit = watchEvent?.CustomMaxWatchCount ?? video.MaxWatchCount;

        var pendingSegments = progressBatch.Segments
            .Where(segment => segment.ProgressSequence > session.LastProgressSequence)
            .ToList();
        if (pendingSegments.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ApiResponse<WatchProgressDto>.Ok(CreateDto(new WatchProgressSnapshot(
                watchEvent,
                maxLimit,
                thresholdSeconds,
                session.ExpiresAt,
                ViewRegistered: false,
                SessionHasRegisteredView: session.HasRegisteredView,
                Duplicate: true)));
        }

        if (progressBatch.IsBatch && pendingSegments[0].ProgressSequence != session.LastProgressSequence + 1)
            return Fail("Progress sequence gap", "PROGRESS_SEQUENCE_GAP");

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

        var remainingSessionWallSeconds = ResolveSessionWallBudget(
            session,
            now,
            pendingSegments.Count);
        var viewRegistered = false;
        foreach (var segment in pendingSegments)
        {
            var progressResult = ApplySessionProgress(new SessionProgressContext(
                segment,
                session,
                watchEvent,
                thresholdSeconds,
                maxLimit,
                now,
                isNewWatchEvent,
                isLocked,
                remainingSessionWallSeconds,
                progressBatch.IsBatch));
            viewRegistered |= progressResult.ViewRegistered;
            remainingSessionWallSeconds = Math.Max(
                0m,
                remainingSessionWallSeconds - progressResult.AcceptedWallSeconds);
        }

        RenewSession(
            session,
            pendingSegments[^1].ProgressSequence,
            effectiveDurationSeconds,
            now);
        watchEvent.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ApiResponse<WatchProgressDto>.Ok(CreateDto(new WatchProgressSnapshot(
            watchEvent,
            maxLimit,
            thresholdSeconds,
            session.ExpiresAt,
            viewRegistered,
            session.HasRegisteredView,
            Duplicate: false)));
    }

    private async Task<SessionTrackingPolicy?> ResolveTrackingPolicyAsync(
        VideoPlaybackSession session,
        LessonVideo video,
        TrackWatchProgressCommand request,
        CancellationToken ct)
    {
        var durationSeconds = await ResolveSessionDurationAsync(
            session,
            video,
            request.TotalDurationSeconds,
            ct);
        if (durationSeconds is null)
            return null;

        var thresholdPercentage = session.TrackingThresholdPercentage is >= 1 and <= 100
            ? session.TrackingThresholdPercentage.Value
            : await ResolveCurrentThresholdPercentageAsync(video.Provider, ct);
        var thresholdSeconds = session.TrackingThresholdSeconds is > 0
            ? session.TrackingThresholdSeconds.Value
            : VideoWatchProgressCalculator.ResolveThresholdSeconds(
                durationSeconds.Value,
                thresholdPercentage);

        session.TrackingDurationSeconds = durationSeconds.Value;
        session.TrackingThresholdPercentage = thresholdPercentage;
        session.TrackingThresholdSeconds = thresholdSeconds;

        return new SessionTrackingPolicy(durationSeconds.Value, thresholdSeconds);
    }

    private async Task<int?> ResolveSessionDurationAsync(
        VideoPlaybackSession session,
        LessonVideo video,
        int reportedDurationSeconds,
        CancellationToken ct)
    {
        if (session.TrackingDurationSeconds is > 0)
            return session.TrackingDurationSeconds;

        if (VideoProviders.Normalize(video.Provider) == VideoProviders.Bunny)
        {
            var bunnyDurationSeconds = await _db.BunnyVideoAssets
                .Where(asset => asset.LessonVideoId == video.Id
                                && asset.SourceState == BunnyVideoAssetSourceState.Current
                                && asset.DurationSeconds > 0)
                .Select(asset => asset.DurationSeconds)
                .SingleOrDefaultAsync(ct);
            if (bunnyDurationSeconds is > 0)
                return bunnyDurationSeconds;
        }

        return reportedDurationSeconds > 0 ? reportedDurationSeconds : null;
    }

    private async Task<int> ResolveCurrentThresholdPercentageAsync(
        string provider,
        CancellationToken ct)
    {
        var settings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var normalizedProvider = VideoProviders.Normalize(provider);
        var providerKey = normalizedProvider == VideoProviders.Bunny
            ? PlatformSettingKeys.BunnyWatchThresholdPercentage
            : normalizedProvider == VideoProviders.YouTube
                ? PlatformSettingKeys.YouTubeWatchThresholdPercentage
                : PlatformSettingKeys.VideoWatchThresholdPercentage;
        var providerThreshold = await _db.PlatformSettings
            .AsNoTracking()
            .Where(setting => setting.Key == providerKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(providerThreshold, out var configuredThreshold)
            ? Math.Clamp(configuredThreshold, 1, 100)
            : Math.Clamp(settings.VideoWatchThresholdPercentage, 1, 100);
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

    private static SessionProgressResult ApplySessionProgress(SessionProgressContext context)
    {
        if (context.IsLocked || context.Session.HasRegisteredView)
            return new SessionProgressResult(false, 0m);

        var candidateAcceptedSeconds = context.IsBatch
            ? VideoWatchProgressCalculator.SanitizeReportedSeconds(context.Segment.SecondsWatched)
            : VideoWatchProgressCalculator.ResolveAcceptedSeconds(
                context.Segment.SecondsWatched,
                context.Now,
                context.WatchEvent,
                context.IsNewWatchEvent);
        var actualAcceptedSeconds = Math.Min(
            candidateAcceptedSeconds,
            context.MaxAcceptedWallSeconds);

        var playbackRate = (decimal)context.Segment.PlaybackRate;
        context.Session.AcceptedWallSeconds += actualAcceptedSeconds;
        var speedAdjustedSeconds =
            (actualAcceptedSeconds * playbackRate) + context.Session.SpeedAdjustedSecondsRemainder;
        var acceptedSeconds = decimal.ToInt32(decimal.Floor(speedAdjustedSeconds));
        var nextRemainder = speedAdjustedSeconds - acceptedSeconds;
        var boundedSeconds = VideoWatchProgressCalculator.CapAtNextViewBoundary(
            context.WatchEvent,
            acceptedSeconds,
            context.ThresholdSeconds);
        var progress = VideoWatchProgressCalculator.ApplyProgress(
            context.WatchEvent,
            boundedSeconds,
            context.ThresholdSeconds,
            context.MaxLimit);
        var acceptedWallSeconds = progress.ViewRegistered
            ? Math.Min(
                actualAcceptedSeconds,
                Math.Max(0m, boundedSeconds - context.Session.SpeedAdjustedSecondsRemainder) / playbackRate)
            : actualAcceptedSeconds;
        context.WatchEvent.ActualWatchedSeconds += acceptedWallSeconds;
        context.WatchEvent.LastPlaybackRate = playbackRate;
        var breakdown = JsonSerializer.Deserialize<Dictionary<string, decimal>>(context.WatchEvent.PlaybackRateBreakdownJson) ?? new();
        var rateKey = context.Segment.PlaybackRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        breakdown[rateKey] = breakdown.GetValueOrDefault(rateKey) + acceptedWallSeconds;
        context.WatchEvent.PlaybackRateBreakdownJson = JsonSerializer.Serialize(breakdown);
        context.Session.HasRegisteredView = progress.ViewRegistered;
        context.Session.SpeedAdjustedSecondsRemainder = progress.ViewRegistered
            ? 0m
            : nextRemainder;
        return new SessionProgressResult(progress.ViewRegistered, actualAcceptedSeconds);
    }

    private static ProgressBatchValidation ValidateProgressBatch(TrackWatchProgressCommand request)
    {
        if (request.ProgressSegments is null)
        {
            if (request.ProgressSequence <= 0)
                return ProgressBatchValidation.Invalid("Progress sequence required", "PROGRESS_SEQUENCE_REQUIRED");
            if (!IsSupportedPlaybackRate(request.PlaybackRate))
                return ProgressBatchValidation.Invalid("Invalid playback rate", "PLAYBACK_RATE_INVALID");

            return ProgressBatchValidation.Valid(new ProgressBatch(
                [new WatchProgressSegment(
                    request.ProgressSequence,
                    request.SecondsWatched,
                    request.PlaybackRate)],
                IsBatch: false));
        }

        if (request.ProgressSegments.Count == 0)
            return ProgressBatchValidation.Invalid("Progress segments required", "PROGRESS_SEGMENTS_REQUIRED");
        if (request.ProgressSegments.Count > MaxProgressSegmentsPerRequest)
            return ProgressBatchValidation.Invalid("Too many progress segments", "PROGRESS_SEGMENTS_LIMIT_EXCEEDED");

        long? previousSequence = null;
        foreach (var segment in request.ProgressSegments)
        {
            if (segment.ProgressSequence <= 0)
                return ProgressBatchValidation.Invalid("Progress sequence required", "PROGRESS_SEQUENCE_REQUIRED");
            if (!double.IsFinite(segment.SecondsWatched)
                || segment.SecondsWatched <= 0
                || segment.SecondsWatched > MaxSecondsPerProgressSegment)
            {
                return ProgressBatchValidation.Invalid("Invalid watched seconds", "PROGRESS_SECONDS_INVALID");
            }
            if (!IsSupportedPlaybackRate(segment.PlaybackRate))
                return ProgressBatchValidation.Invalid("Invalid playback rate", "PLAYBACK_RATE_INVALID");
            if (previousSequence.HasValue
                && (segment.ProgressSequence <= previousSequence.Value
                    || segment.ProgressSequence - previousSequence.Value != 1))
            {
                return ProgressBatchValidation.Invalid("Progress segments must be consecutive", "PROGRESS_SEQUENCE_GAP");
            }

            previousSequence = segment.ProgressSequence;
        }

        return ProgressBatchValidation.Valid(new ProgressBatch(request.ProgressSegments, IsBatch: true));
    }

    private static decimal ResolveSessionWallBudget(
        VideoPlaybackSession session,
        DateTime now,
        int pendingSegmentCount)
    {
        var elapsedSeconds = Math.Max(0m, (decimal)(now - session.CreatedAt).TotalSeconds);
        var maxByClock = Math.Max(0m, elapsedSeconds - session.AcceptedWallSeconds);
        var maxBySegmentCount = (decimal)pendingSegmentCount * MaxSecondsPerProgressSegment;
        return Math.Min(maxByClock, maxBySegmentCount);
    }

    private static bool IsSupportedPlaybackRate(double playbackRate) =>
        playbackRate is 0.5 or 0.75 or 1 or 1.25 or 1.5 or 1.75 or 2;

    private static void RenewSession(
        VideoPlaybackSession session,
        long progressSequence,
        int totalDurationSeconds,
        DateTime now)
    {
        session.LastProgressSequence = progressSequence;
        session.LastProgressAt = now;
        session.ExpiresAt = now.Add(VideoPlaybackSessionPolicy.ResolveLifetime(totalDurationSeconds));
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
            snapshot.SessionHasRegisteredView,
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
        bool SessionHasRegisteredView,
        bool Duplicate);

    private sealed record SessionProgressContext(
        WatchProgressSegment Segment,
        VideoPlaybackSession Session,
        VideoWatchEvent WatchEvent,
        int ThresholdSeconds,
        int MaxLimit,
        DateTime Now,
        bool IsNewWatchEvent,
        bool IsLocked,
        decimal MaxAcceptedWallSeconds,
        bool IsBatch);

    private sealed record SessionProgressResult(
        bool ViewRegistered,
        decimal AcceptedWallSeconds);

    private sealed record ProgressBatch(
        IReadOnlyList<WatchProgressSegment> Segments,
        bool IsBatch);

    private sealed record ProgressBatchValidation(
        ProgressBatch? Batch,
        string? ErrorMessage,
        string? ErrorCode)
    {
        public bool IsValid => Batch is not null;

        public static ProgressBatchValidation Valid(ProgressBatch batch) =>
            new(batch, null, null);

        public static ProgressBatchValidation Invalid(string message, string code) =>
            new(null, message, code);
    }

    private sealed record SessionTrackingPolicy(
        int DurationSeconds,
        int ThresholdSeconds);
}
