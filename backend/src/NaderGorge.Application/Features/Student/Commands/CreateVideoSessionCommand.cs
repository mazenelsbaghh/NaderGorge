using MediatR;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public enum VideoSessionMode
{
    Standard,
    AdminPreview
}

public record CreateVideoSessionCommand(
    Guid LessonVideoId,
    Guid UserId,
    string? IpAddress = null,
    VideoSessionMode Mode = VideoSessionMode.Standard) : IRequest<ApiResponse<VideoSessionDto>>;

public record VideoSessionDto(
    Guid SessionId,
    DateTime ExpiresAt,
    string Provider,
    WatchInfoDto WatchInfo,
    string VideoTitle,
    int ThresholdPercentage,
    int? DurationSeconds,
    bool IsPreview
);

public record WatchInfoDto(int CurrentCount, int MaxCount, bool IsLocked, int TotalTrackedSeconds);

public class CreateVideoSessionCommandHandler : IRequestHandler<CreateVideoSessionCommand, ApiResponse<VideoSessionDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;
    private readonly IVideoEncryptionService _encryption;
    private readonly IGiftUsageService? _giftUsage;
    private readonly IVideoPlaybackConcurrency? _playbackConcurrency;
    private readonly IBunnyVideoDurationResolver? _bunnyVideoDurationResolver;
    private readonly IBunnyHlsSecretProtector? _bunnyHlsSecretProtector;
    private readonly IBunnyHlsUrlSigner? _bunnyHlsUrlSigner;

    public CreateVideoSessionCommandHandler(
        IAppDbContext db,
        IAccessCheckService access,
        IVideoEncryptionService encryption,
        IGiftUsageService? giftUsage = null,
        IVideoPlaybackConcurrency? playbackConcurrency = null,
        IBunnyVideoDurationResolver? bunnyVideoDurationResolver = null,
        IBunnyHlsSecretProtector? bunnyHlsSecretProtector = null,
        IBunnyHlsUrlSigner? bunnyHlsUrlSigner = null)
    {
        _db = db;
        _access = access;
        _encryption = encryption;
        _giftUsage = giftUsage;
        _playbackConcurrency = playbackConcurrency;
        _bunnyVideoDurationResolver = bunnyVideoDurationResolver;
        _bunnyHlsSecretProtector = bunnyHlsSecretProtector;
        _bunnyHlsUrlSigner = bunnyHlsUrlSigner;
    }

    public async Task<ApiResponse<VideoSessionDto>> Handle(CreateVideoSessionCommand request, CancellationToken ct)
    {
        var isAdminPreview = request.Mode == VideoSessionMode.AdminPreview;
        var video = await _db.LessonVideos
            .Include(v => v.Lesson)
            .Include(v => v.BunnyStreamLibrary)
            .Include(v => v.BunnyVideoAssets)
            .FirstOrDefaultAsync(v => v.Id == request.LessonVideoId, ct);

        if (video == null)
            return ApiResponse<VideoSessionDto>.Fail("Video not found", new List<string> { "VIDEO_NOT_FOUND" });

        // Validate provider
        if (!VideoProviders.IsSupported(video.Provider))
        {
            return ApiResponse<VideoSessionDto>.Fail("Invalid video provider", new List<string> { "INVALID_PROVIDER" });
        }

        // 1. Verify access to the package
        var hasLessonAccess = await _access.HasAccessToLessonAsync(request.UserId, video.LessonId, ct);
        var hasAccess = hasLessonAccess || await _access.HasAccessToVideoAsync(request.UserId, video.Id, ct);
        if (!hasAccess)
            return ApiResponse<VideoSessionDto>.Fail("You do not have access to this video", new List<string> { "ACCESS_DENIED" });

        var normalizedProvider = VideoProviders.Normalize(video.Provider);
        var sessionProvider = video.Provider;
        var encryptedVideoId = video.ProviderVideoId;
        int? knownDurationSeconds = null;
        if (normalizedProvider == VideoProviders.Bunny)
        {
            if (video.BunnyStreamLibrary is null)
            {
                return ApiResponse<VideoSessionDto>.Fail(
                    "هذا الفيديو غير مرتبط بمكتبة Bunny.",
                    new List<string> { "BUNNY_LIBRARY_MISSING" });
            }

            var currentBunnyAsset = video.BunnyVideoAssets
                .SingleOrDefault(asset => asset.SourceState == BunnyVideoAssetSourceState.Current);
            knownDurationSeconds = currentBunnyAsset?.DurationSeconds is > 0
                ? currentBunnyAsset.DurationSeconds
                : null;
            if (currentBunnyAsset is not null
                && !string.Equals(currentBunnyAsset.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<VideoSessionDto>.Fail(
                    "فيديو Bunny ما زال قيد الرفع أو المعالجة.",
                    new List<string> { "BUNNY_VIDEO_NOT_READY" });
            }

            if (knownDurationSeconds is null && _bunnyVideoDurationResolver is not null)
            {
                knownDurationSeconds = await _bunnyVideoDurationResolver.ResolveAsync(
                    video.BunnyStreamLibrary.Id,
                    video.ProviderVideoId,
                    ct);
            }

            encryptedVideoId = $"{video.BunnyStreamLibrary.ExternalLibraryId}/{video.ProviderVideoId}";
        }

        var thresholdPercentage = await ResolveThresholdPercentageAsync(normalizedProvider, ct);
        var thresholdSeconds = knownDurationSeconds is > 0
            ? VideoWatchProgressCalculator.ResolveThresholdSeconds(
                knownDurationSeconds.Value,
                thresholdPercentage)
            : (int?)null;

        var hasNonGiftVideoAccess = false;
        if (!hasLessonAccess)
        {
            var videoAccessContext = await _db.LessonVideos
                .AsNoTracking()
                .Where(v => v.Id == request.LessonVideoId)
                .Select(v => new
                {
                    v.LessonId,
                    v.VideoTypeId,
                    ContentSectionId = v.Lesson.ContentSectionId,
                    TermId = v.Lesson.ContentSection.TermId,
                    PackageId = v.Lesson.ContentSection.Term.PackageId,
                    TeacherId = v.Lesson.ContentSection.Term.Package.TeacherId
                })
                .FirstOrDefaultAsync(ct);

            if (videoAccessContext != null)
            {
                var nowForGrantCheck = DateTime.UtcNow;
                hasNonGiftVideoAccess = await _db.StudentAccessGrants.AnyAsync(g =>
                    g.UserId == request.UserId &&
                    g.GiftRecipientId == null &&
                    g.IsActive &&
                    g.GrantType == CodeType.Video &&
                    (g.ExpiresAt == null || g.ExpiresAt > nowForGrantCheck) &&
                    (g.MaxUses == null || g.UsesConsumed < g.MaxUses) &&
                    (
                        g.LessonVideoId == request.LessonVideoId ||
                        (
                            g.VideoTypeId != null &&
                            g.VideoTypeId == videoAccessContext.VideoTypeId &&
                            (g.LessonId == null || g.LessonId == videoAccessContext.LessonId) &&
                            (g.ContentSectionId == null || g.ContentSectionId == videoAccessContext.ContentSectionId) &&
                            (g.TermId == null || g.TermId == videoAccessContext.TermId) &&
                            (g.PackageId == null || g.PackageId == videoAccessContext.PackageId) &&
                            (
                                g.AccessCode == null ||
                                g.AccessCode.CodeGroup.TeacherId == null ||
                                g.AccessCode.CodeGroup.TeacherId == videoAccessContext.TeacherId
                            )
                        )
                    ), ct);
            }
        }

        // 1b. Check if the current video has an unpassed mandatory exam
        var videoExams = await _db.Exams
            .Where(e => e.IsActive && e.IsMandatory && (
                e.LessonVideoId == video.Id ||
                (video.ExamId == e.Id)
            ))
            .Select(e => e.Id)
            .ToListAsync(ct);

        if (!isAdminPreview && videoExams.Any())
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

        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (_playbackConcurrency is not null)
            await _playbackConcurrency.AcquireAsync(request.UserId, request.LessonVideoId, ct);
        var now = DateTime.UtcNow;

        // 2. Check watch limits under the same per-student/video lock used by
        // progress tracking, so a just-registered final view cannot race a new
        // playback session into existence.
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

        if (isLocked && !isStaffOrTeacher && !isAdminPreview)
        {
            // Also ensure the flag is persisted so future checks are fast
            if (watchEvent != null && !watchEvent.IsLocked)
            {
                watchEvent.IsLocked = true;
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

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
                thresholdPercentage,
                knownDurationSeconds,
                IsPreview: false);
            return ApiResponse<VideoSessionDto>.Fail("Watch limit reached for this video", new List<string> { "WATCH_LIMIT_REACHED" }, lockedDto);
        }
        else
        {
            // Self-repair out-of-sync DB flag
            if (!isAdminPreview && watchEvent != null && watchEvent.IsLocked)
            {
                watchEvent.IsLocked = false;
            }
        }

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
            ExpiresAt = now.Add(VideoPlaybackSessionPolicy.ResolveLifetime(knownDurationSeconds)),
            IsConsumed = false,
            HasRegisteredView = false,
            LastProgressSequence = 0,
            IsSuperseded = false,
            IpAddress = request.IpAddress,
            TrackingDurationSeconds = knownDurationSeconds,
            TrackingThresholdPercentage = thresholdPercentage,
            TrackingThresholdSeconds = thresholdSeconds,
            SpeedAdjustedSecondsRemainder = 0m,
            AcceptedWallSeconds = 0m
        };

        string studentName = user?.FullName ?? "Unknown";
        string studentPhone = user?.PhoneNumber ?? "Unknown";
        if (normalizedProvider == VideoProviders.Bunny
            && video.BunnyPlaybackMode == BunnyPlaybackMode.PlatformHls
            && video.BunnyStreamLibrary?.HlsTokenKeyCiphertext is { Length: > 0 } tokenCiphertext
            && !string.IsNullOrWhiteSpace(video.BunnyStreamLibrary.HlsCdnHostname)
            && _bunnyHlsSecretProtector is not null
            && _bunnyHlsUrlSigner is not null)
        {
            try
            {
                var tokenKey = _bunnyHlsSecretProtector.Unprotect(video.BunnyStreamLibrary.Id, tokenCiphertext);
                encryptedVideoId = _bunnyHlsUrlSigner.SignPlaylist(
                    video.BunnyStreamLibrary.HlsCdnHostname,
                    video.ProviderVideoId,
                    tokenKey,
                    session.ExpiresAt);
                sessionProvider = "bunny-hls";
            }
            catch (Exception exception) when (exception is CryptographicException or InvalidOperationException or ArgumentException)
            {
                // Preserve playback for existing content if a library HLS secret
                // was rotated or is incomplete; the standard Bunny player remains available.
                sessionProvider = video.Provider;
            }
        }
        session.SessionToken = _encryption.EncryptVideoInfo(
            sessionProvider,
            encryptedVideoId,
            session.EncryptionKey,
            studentName,
            studentPhone);

        _db.VideoPlaybackSessions.Add(session);
        if (!isAdminPreview && !hasLessonAccess && !hasNonGiftVideoAccess && _giftUsage != null)
        {
            var consumed = await _giftUsage.TryConsumeAsync(
                request.UserId,
                GiftTargetType.Video,
                request.LessonVideoId,
                ct);
            if (!consumed)
            {
                await transaction.RollbackAsync(ct);
                return ApiResponse<VideoSessionDto>.Fail(
                    "The gifted video access is no longer available.",
                    new List<string> { "GIFT_LIMIT_REACHED" });
            }
        }
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var sessionWatchInfo = isAdminPreview
            ? new WatchInfoDto(0, 0, IsLocked: false, TotalTrackedSeconds: 0)
            : new WatchInfoDto(currentCount, maxCount, isLocked, Math.Max(0, watchEvent?.TimeWatchedInSeconds ?? 0));
        var dto = new VideoSessionDto(
            session.Id,
            session.ExpiresAt,
            sessionProvider,
            sessionWatchInfo,
            video.Title,
            thresholdPercentage,
            knownDurationSeconds,
            isAdminPreview
        );

        return ApiResponse<VideoSessionDto>.Ok(dto);
    }

    private async Task<int> ResolveThresholdPercentageAsync(
        string normalizedProvider,
        CancellationToken ct)
    {
        var providerKey = normalizedProvider == VideoProviders.Bunny
            ? PlatformSettingKeys.BunnyWatchThresholdPercentage
            : normalizedProvider == VideoProviders.YouTube
                ? PlatformSettingKeys.YouTubeWatchThresholdPercentage
                : PlatformSettingKeys.VideoWatchThresholdPercentage;
        var globalKey = PlatformSettingKeys.VideoWatchThresholdPercentage;
        var keys = new[] { providerKey, globalKey }.Distinct().ToList();
        var configuredThresholds = await _db.PlatformSettings
            .AsNoTracking()
            .Where(setting => keys.Contains(setting.Key))
            .Select(setting => new { setting.Key, setting.Value })
            .ToListAsync(ct);

        var providerValue = configuredThresholds
            .FirstOrDefault(setting => setting.Key == providerKey)?.Value;
        if (int.TryParse(providerValue, out var providerThreshold))
            return Math.Clamp(providerThreshold, 1, 100);

        var globalValue = configuredThresholds
            .FirstOrDefault(setting => setting.Key == globalKey)?.Value;
        return int.TryParse(globalValue, out var globalThreshold)
            ? Math.Clamp(globalThreshold, 1, 100)
            : CachedPlatformSettings.Default.VideoWatchThresholdPercentage;
    }
}
