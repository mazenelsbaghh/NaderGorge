using System.Security.Cryptography;
using System.Data;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateBunnyTusUploadCommand(
    Guid? TeacherId,
    Guid? PackageId,
    Guid LessonId,
    string Title,
    int Order,
    int MaxWatchCount,
    Guid VideoTypeId,
    Guid BunnyStreamLibraryId,
    bool IsActive,
    string? FileName,
    long? FileSizeBytes,
    Guid CurrentUserId,
    Guid? ExistingLessonVideoId = null) : IRequest<ApiResponse<BunnyTusUploadSessionDto>>;

public record BunnyTusUploadSessionDto(
    Guid LessonVideoId,
    Guid BunnyVideoAssetId,
    string BunnyVideoGuid,
    long LibraryId,
    string TusEndpoint,
    string AuthorizationSignature,
    long AuthorizationExpire,
    Dictionary<string, string> UploadHeaders,
    string Status);

public record CompleteBunnyUploadCommand(Guid AssetId, Guid CurrentUserId) : IRequest<ApiResponse<BunnyUploadStatusDto>>;

public record CancelBunnyVideoReplacementCommand(Guid AssetId, Guid CurrentUserId) : IRequest<ApiResponse<BunnyUploadStatusDto>>;

public record FetchBunnyVideoCommand(
    Guid? TeacherId,
    Guid? PackageId,
    Guid LessonId,
    string Title,
    int Order,
    int MaxWatchCount,
    Guid VideoTypeId,
    Guid BunnyStreamLibraryId,
    bool IsActive,
    string SourceUrl,
    Guid CurrentUserId,
    Guid? ExistingLessonVideoId = null) : IRequest<ApiResponse<BunnyUploadStatusDto>>;

public record RefreshBunnyVideoStatusCommand(Guid AssetId, Guid CurrentUserId) : IRequest<ApiResponse<BunnyUploadStatusDto>>;

public record RefreshPendingBunnyVideosCommand(int BatchSize = 25) : IRequest<BunnyPendingRefreshResultDto>;

public sealed record BunnyPendingRefreshResultDto(int Attempted, int Refreshed, int Failed);

public record BunnyUploadStatusDto(Guid AssetId, Guid LessonVideoId, string Status, int? EncodeProgress, string? Message);

internal sealed record BunnyUploadReplacementTarget(
    LessonVideo? LessonVideo,
    int? ExpectedSourceRevision,
    string? ErrorMessage,
    string? ErrorCode)
{
    public bool IsReplacement => LessonVideo is not null;
    public bool Success => ErrorMessage is null;
}

internal static class BunnyUploadReplacementTargetResolver
{
    public static async Task<BunnyUploadReplacementTarget> ResolveAsync(
        IAppDbContext db,
        Guid? existingLessonVideoId,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        if (!existingLessonVideoId.HasValue)
        {
            return new BunnyUploadReplacementTarget(null, null, null, null);
        }

        var existingVideo = await db.LessonVideos
            .Include(video => video.BunnyVideoAssets)
            .FirstOrDefaultAsync(video => video.Id == existingLessonVideoId.Value, cancellationToken);
        if (existingVideo is null || existingVideo.LessonId != lessonId)
        {
            return new BunnyUploadReplacementTarget(
                null,
                null,
                "الفيديو المطلوب استبداله غير موجود ضمن هذا الدرس.",
                "BUNNY_REPLACEMENT_VIDEO_INVALID");
        }

        if (existingVideo.BunnyVideoAssets.Any(asset => asset.SourceState == BunnyVideoAssetSourceState.PendingReplacement))
        {
            var expiredAny = BunnyVideoReplacementLifecycle.ExpirePendingReplacements(
                existingVideo.BunnyVideoAssets,
                DateTime.UtcNow);
            if (expiredAny)
            {
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    db.ClearTrackedChanges();
                    return new BunnyUploadReplacementTarget(
                        null,
                        null,
                        "تغيرت حالة استبدال Bunny أثناء المعالجة. أعد المحاولة.",
                        "BUNNY_REPLACEMENT_CONFLICT");
                }
            }

            if (!existingVideo.BunnyVideoAssets.Any(asset => asset.SourceState == BunnyVideoAssetSourceState.PendingReplacement))
            {
                return new BunnyUploadReplacementTarget(existingVideo, existingVideo.SourceRevision, null, null);
            }

            return new BunnyUploadReplacementTarget(
                null,
                null,
                "يوجد استبدال فيديو Bunny قيد التجهيز لهذا الفيديو.",
                "BUNNY_REPLACEMENT_PENDING");
        }

        return new BunnyUploadReplacementTarget(existingVideo, existingVideo.SourceRevision, null, null);
    }

    public static Task<bool> IsSourceRevisionCurrentAsync(
        IAppDbContext db,
        BunnyUploadReplacementTarget replacementTarget,
        CancellationToken cancellationToken)
    {
        if (!replacementTarget.IsReplacement)
        {
            return Task.FromResult(true);
        }

        return db.LessonVideos
            .AsNoTracking()
            .AnyAsync(
                video => video.Id == replacementTarget.LessonVideo!.Id
                    && replacementTarget.ExpectedSourceRevision.HasValue
                    && video.SourceRevision == replacementTarget.ExpectedSourceRevision.Value,
                cancellationToken);
    }
}

internal static class BunnyUploadVideoTypeRules
{
    public static async Task<bool> IsAllowedAsync(
        IAppDbContext db,
        BunnyUploadReplacementTarget replacementTarget,
        Guid requestedVideoTypeId,
        CancellationToken cancellationToken)
    {
        // Updating a video may retain its now-disabled type. This mirrors UpdateVideo,
        // while still requiring any newly selected type to be active.
        return replacementTarget.LessonVideo?.VideoTypeId == requestedVideoTypeId
            || await VideoTypeRules.IsActiveAsync(db, requestedVideoTypeId, cancellationToken);
    }
}

public sealed class CreateBunnyTusUploadCommandHandler : IRequestHandler<CreateBunnyTusUploadCommand, ApiResponse<BunnyTusUploadSessionDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _libraries;
    private readonly IBunnyStreamClientFactory _clients;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateBunnyTusUploadCommandHandler> _logger;

    public CreateBunnyTusUploadCommandHandler(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients,
        IConfiguration configuration,
        ILogger<CreateBunnyTusUploadCommandHandler> logger)
    {
        _db = db;
        _libraries = libraries;
        _clients = clients;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<BunnyTusUploadSessionDto>> Handle(CreateBunnyTusUploadCommand request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail("عنوان الفيديو مطلوب ولا يزيد عن 200 حرف.");
        }

        if (request.Order < 1)
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail("ترتيب الفيديو يجب أن يكون 1 أو أكبر.");
        }

        if (request.MaxWatchCount < 0)
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail("حد المشاهدة لا يمكن أن يكون سالبًا.");
        }

        var ownership = await BunnyUploadAuthorization.ResolveAsync(_db, request.CurrentUserId, request.TeacherId, request.PackageId, request.LessonId, cancellationToken);
        if (!ownership.Success)
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail(ownership.Message);
        }

        var replacementTarget = await BunnyUploadReplacementTargetResolver.ResolveAsync(
            _db,
            request.ExistingLessonVideoId,
            request.LessonId,
            cancellationToken);
        if (!replacementTarget.Success)
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail(
                replacementTarget.ErrorMessage!,
                [replacementTarget.ErrorCode!]);
        }

        if (!await BunnyUploadVideoTypeRules.IsAllowedAsync(
                _db,
                replacementTarget,
                request.VideoTypeId,
                cancellationToken))
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail("اختر نوع فيديو نشطاً.", ["VIDEO_TYPE_INVALID"]);
        }

        var libraryResult = await _libraries.ResolveAsync(request.BunnyStreamLibraryId, requireActive: true, cancellationToken);
        if (!libraryResult.Success || libraryResult.Access is null)
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail(
                libraryResult.Message ?? "مكتبة Bunny المحددة غير متاحة.",
                [libraryResult.ErrorCode ?? "BUNNY_LIBRARY_UNAVAILABLE"]);
        }

        var bunny = _clients.Create(libraryResult.Access.ExternalLibraryId, libraryResult.Access.ApiKey);
        BunnyStreamVideoDto bunnyVideo;
        try
        {
            bunnyVideo = await bunny.CreateVideoAsync(title, collectionId: null, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                "Bunny TUS video creation failed in library {BunnyLibraryId}. Failure type: {FailureType}.",
                libraryResult.Access.ExternalLibraryId,
                exception.GetType().Name);
            return ApiResponse<BunnyTusUploadSessionDto>.Fail(
                "تعذر إنشاء الفيديو داخل مكتبة Bunny المحددة.",
                ["BUNNY_CREATE_FAILED"]);
        }

        if (!await BunnyUploadReplacementTargetResolver.IsSourceRevisionCurrentAsync(
                _db,
                replacementTarget,
                cancellationToken))
        {
            await BunnyRemoteVideoCompensation.DeleteBestEffortAsync(
                bunny,
                libraryResult.Access.ExternalLibraryId,
                bunnyVideo.Guid,
                "TUS replacement target changed",
                _logger);
            return ApiResponse<BunnyTusUploadSessionDto>.Fail(
                "تم تغيير مصدر الفيديو أثناء تجهيز استبدال Bunny. أعد المحاولة.",
                ["BUNNY_REPLACEMENT_SOURCE_CHANGED"]);
        }

        LessonVideo lessonVideo;
        BunnyVideoAsset asset;
        BunnyTusUploadSignatureDto signature;
        try
        {
            var expiryMinutes = int.TryParse(_configuration["BunnyStream:TusUploadExpiryMinutes"], out var parsed) ? parsed : 180;
            signature = bunny.CreateTusUploadSignature(bunnyVideo.Guid, TimeSpan.FromMinutes(expiryMinutes));

            lessonVideo = replacementTarget.LessonVideo ?? new LessonVideo
            {
                Title = title,
                Provider = VideoProviders.Bunny,
                ProviderVideoId = bunnyVideo.Guid,
                Order = request.Order,
                MaxWatchCount = request.MaxWatchCount,
                LessonId = request.LessonId,
                VideoTypeId = request.VideoTypeId,
                IsActive = false,
                BunnyStreamLibraryId = libraryResult.Access.Id
            };
            if (!replacementTarget.IsReplacement)
            {
                _db.LessonVideos.Add(lessonVideo);
            }

            asset = new BunnyVideoAsset
            {
                LessonVideo = lessonVideo,
                TeacherId = ownership.TeacherId,
                PackageId = ownership.PackageId,
                LessonId = request.LessonId,
                UploadedByUserId = request.CurrentUserId,
                BunnyLibraryId = bunnyVideo.VideoLibraryId,
                BunnyVideoGuid = bunnyVideo.Guid,
                Title = title,
                UploadMethod = "TusFile",
                Status = "Created",
                OriginalFileName = request.FileName,
                FileSizeBytes = request.FileSizeBytes,
                BunnyEncodeProgress = bunnyVideo.EncodeProgress,
                StorageBytes = bunnyVideo.StorageSize,
                DurationSeconds = bunnyVideo.Length,
                ActivateWhenReady = request.IsActive,
                SourceState = replacementTarget.IsReplacement
                    ? BunnyVideoAssetSourceState.PendingReplacement
                    : BunnyVideoAssetSourceState.Current,
                BunnyStreamLibraryRecordId = libraryResult.Access.Id,
                TargetOrder = replacementTarget.IsReplacement ? request.Order : null,
                TargetMaxWatchCount = replacementTarget.IsReplacement ? request.MaxWatchCount : null,
                TargetVideoTypeId = replacementTarget.IsReplacement ? request.VideoTypeId : null,
                TargetIsActive = replacementTarget.IsReplacement ? request.IsActive : null,
                TargetSourceRevision = replacementTarget.IsReplacement
                    ? replacementTarget.ExpectedSourceRevision
                    : null
            };
            _db.BunnyVideoAssets.Add(asset);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Local persistence failed after creating a Bunny TUS video in library {BunnyLibraryId}; remote cleanup was requested. Failure type: {FailureType}.",
                libraryResult.Access.ExternalLibraryId,
                exception.GetType().Name);
            await BunnyRemoteVideoCompensation.DeleteBestEffortAsync(
                bunny,
                libraryResult.Access.ExternalLibraryId,
                bunnyVideo.Guid,
                "TUS setup",
                _logger);
            throw;
        }

        var headers = new Dictionary<string, string>
        {
            ["AuthorizationSignature"] = signature.AuthorizationSignature,
            ["AuthorizationExpire"] = signature.AuthorizationExpire.ToString(),
            ["LibraryId"] = signature.LibraryId.ToString(),
            ["VideoId"] = signature.VideoId
        };

        return ApiResponse<BunnyTusUploadSessionDto>.Ok(new BunnyTusUploadSessionDto(
            lessonVideo.Id,
            asset.Id,
            bunnyVideo.Guid,
            signature.LibraryId,
            signature.TusEndpoint,
            signature.AuthorizationSignature,
            signature.AuthorizationExpire,
            headers,
            asset.Status));
    }
}

public sealed class CompleteBunnyUploadCommandHandler : IRequestHandler<CompleteBunnyUploadCommand, ApiResponse<BunnyUploadStatusDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _libraries;
    private readonly IBunnyStreamClientFactory _clients;

    public CompleteBunnyUploadCommandHandler(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients)
    {
        _db = db;
        _libraries = libraries;
        _clients = clients;
    }

    public async Task<ApiResponse<BunnyUploadStatusDto>> Handle(CompleteBunnyUploadCommand request, CancellationToken cancellationToken)
    {
        var asset = await _db.BunnyVideoAssets.Include(a => a.LessonVideo).FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);
        if (asset is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Bunny asset not found.");
        }

        if (asset.SourceState == BunnyVideoAssetSourceState.Retired)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                "تمت أرشفة أصل Bunny هذا ولا يمكن تحديثه.",
                ["BUNNY_ASSET_RETIRED"]);
        }

        var canAccess = await BunnyUploadAuthorization.CanAccessAssetAsync(_db, request.CurrentUserId, asset, cancellationToken);
        if (!canAccess)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Unauthorized access to this Bunny upload.");
        }

        if (BunnyVideoReplacementLifecycle.ExpireIfNeeded(asset, DateTime.UtcNow))
        {
            if (!await BunnyVideoReplacementLifecycle.TrySaveAssetStateAsync(_db, asset, cancellationToken))
            {
                return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto(null));
            }

            return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto("انتهت مهلة الاستبدال قبل اكتمال Bunny."));
        }

        var bunnyResult = await BunnyUploadClientResolver.ResolveAsync(_libraries, _clients, asset, cancellationToken);
        if (!bunnyResult.Success || bunnyResult.Client is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                bunnyResult.Message ?? "تعذر الوصول إلى مكتبة Bunny المرتبطة بالفيديو.",
                [bunnyResult.ErrorCode ?? "BUNNY_LIBRARY_UNAVAILABLE"]);
        }

        await BunnyUploadStatusUpdater.RefreshAsync(bunnyResult.Client, asset, cancellationToken);
        var replacementWasApplied = await BunnyVideoReplacementLifecycle.FinalizeIfNeededAsync(
            _db,
            asset,
            cancellationToken);
        if (!replacementWasApplied)
        {
            await BunnyVideoReplacementLifecycle.TrySaveAssetStateAsync(_db, asset, cancellationToken);
        }
        return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto(null));
    }
}

public sealed class CancelBunnyVideoReplacementCommandHandler
    : IRequestHandler<CancelBunnyVideoReplacementCommand, ApiResponse<BunnyUploadStatusDto>>
{
    private readonly IAppDbContext _db;

    public CancelBunnyVideoReplacementCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<BunnyUploadStatusDto>> Handle(
        CancelBunnyVideoReplacementCommand request,
        CancellationToken cancellationToken)
    {
        var asset = await _db.BunnyVideoAssets
            .Include(item => item.LessonVideo)
            .FirstOrDefaultAsync(item => item.Id == request.AssetId, cancellationToken);
        if (asset is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Bunny asset not found.");
        }

        var canAccess = await BunnyUploadAuthorization.CanAccessAssetAsync(
            _db,
            request.CurrentUserId,
            asset,
            cancellationToken);
        if (!canAccess)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Unauthorized access to this Bunny replacement.");
        }

        if (asset.SourceState != BunnyVideoAssetSourceState.PendingReplacement)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                "هذا الأصل ليس استبدال Bunny قيد التجهيز.",
                ["BUNNY_REPLACEMENT_NOT_PENDING"]);
        }

        BunnyVideoReplacementLifecycle.Cancel(asset, request.CurrentUserId);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _db.Entry(asset).ReloadAsync(cancellationToken);
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                "هذا الأصل ليس استبدال Bunny قيد التجهيز.",
                ["BUNNY_REPLACEMENT_NOT_PENDING"]);
        }

        return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto("تم إلغاء استبدال Bunny مع الاحتفاظ بالأصل للمراجعة."));
    }
}

public sealed class FetchBunnyVideoCommandHandler : IRequestHandler<FetchBunnyVideoCommand, ApiResponse<BunnyUploadStatusDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _libraries;
    private readonly IBunnyStreamClientFactory _clients;
    private readonly ILogger<FetchBunnyVideoCommandHandler> _logger;

    public FetchBunnyVideoCommandHandler(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients,
        ILogger<FetchBunnyVideoCommandHandler> logger)
    {
        _db = db;
        _libraries = libraries;
        _clients = clients;
        _logger = logger;
    }

    public async Task<ApiResponse<BunnyUploadStatusDto>> Handle(FetchBunnyVideoCommand request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("عنوان الفيديو مطلوب ولا يزيد عن 200 حرف.");
        }

        if (request.Order < 1)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("ترتيب الفيديو يجب أن يكون 1 أو أكبر.");
        }

        if (request.MaxWatchCount < 0)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("حد المشاهدة لا يمكن أن يكون سالبًا.");
        }

        if (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Remote video URL must be a valid HTTP/HTTPS URL.");
        }

        var ownership = await BunnyUploadAuthorization.ResolveAsync(_db, request.CurrentUserId, request.TeacherId, request.PackageId, request.LessonId, cancellationToken);
        if (!ownership.Success)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(ownership.Message);
        }

        var replacementTarget = await BunnyUploadReplacementTargetResolver.ResolveAsync(
            _db,
            request.ExistingLessonVideoId,
            request.LessonId,
            cancellationToken);
        if (!replacementTarget.Success)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                replacementTarget.ErrorMessage!,
                [replacementTarget.ErrorCode!]);
        }

        if (!await BunnyUploadVideoTypeRules.IsAllowedAsync(
                _db,
                replacementTarget,
                request.VideoTypeId,
                cancellationToken))
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("اختر نوع فيديو نشطاً.", ["VIDEO_TYPE_INVALID"]);
        }

        var libraryResult = await _libraries.ResolveAsync(request.BunnyStreamLibraryId, requireActive: true, cancellationToken);
        if (!libraryResult.Success || libraryResult.Access is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                libraryResult.Message ?? "مكتبة Bunny المحددة غير متاحة.",
                [libraryResult.ErrorCode ?? "BUNNY_LIBRARY_UNAVAILABLE"]);
        }

        var bunny = _clients.Create(libraryResult.Access.ExternalLibraryId, libraryResult.Access.ApiKey);
        BunnyStreamVideoDto bunnyVideo;
        try
        {
            bunnyVideo = await bunny.CreateVideoAsync(title, collectionId: null, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                "Bunny URL-fetch placeholder creation failed in library {BunnyLibraryId}. Failure type: {FailureType}.",
                libraryResult.Access.ExternalLibraryId,
                exception.GetType().Name);
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                "تعذر بدء جلب الفيديو داخل مكتبة Bunny المحددة.",
                ["BUNNY_FETCH_FAILED"]);
        }

        BunnyFetchVideoResultDto fetchResult;
        try
        {
            fetchResult = await bunny.FetchVideoAsync(bunnyVideo.Guid, request.SourceUrl, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                "Bunny URL fetch failed in library {BunnyLibraryId}; remote cleanup was requested. Failure type: {FailureType}.",
                libraryResult.Access.ExternalLibraryId,
                exception.GetType().Name);
            await BunnyRemoteVideoCompensation.DeleteBestEffortAsync(
                bunny,
                libraryResult.Access.ExternalLibraryId,
                bunnyVideo.Guid,
                "URL fetch",
                _logger);
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                "تعذر بدء جلب الفيديو داخل مكتبة Bunny المحددة.",
                ["BUNNY_FETCH_FAILED"]);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Bunny URL fetch was interrupted in library {BunnyLibraryId}; remote cleanup was requested. Failure type: {FailureType}.",
                libraryResult.Access.ExternalLibraryId,
                exception.GetType().Name);
            await BunnyRemoteVideoCompensation.DeleteBestEffortAsync(
                bunny,
                libraryResult.Access.ExternalLibraryId,
                bunnyVideo.Guid,
                "URL fetch",
                _logger);
            throw;
        }
        if (!fetchResult.Success)
        {
            _logger.LogWarning(
                "Bunny rejected a URL fetch in library {BunnyLibraryId} with status {BunnyStatusCode}; remote cleanup was requested.",
                libraryResult.Access.ExternalLibraryId,
                fetchResult.StatusCode);
            await BunnyRemoteVideoCompensation.DeleteBestEffortAsync(
                bunny,
                libraryResult.Access.ExternalLibraryId,
                bunnyVideo.Guid,
                "URL fetch",
                _logger);
            return ApiResponse<BunnyUploadStatusDto>.Fail(fetchResult.Message ?? "Bunny fetch request failed.");
        }

        if (!await BunnyUploadReplacementTargetResolver.IsSourceRevisionCurrentAsync(
                _db,
                replacementTarget,
                cancellationToken))
        {
            await BunnyRemoteVideoCompensation.DeleteBestEffortAsync(
                bunny,
                libraryResult.Access.ExternalLibraryId,
                bunnyVideo.Guid,
                "URL-fetch replacement target changed",
                _logger);
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                "تم تغيير مصدر الفيديو أثناء تجهيز استبدال Bunny. أعد المحاولة.",
                ["BUNNY_REPLACEMENT_SOURCE_CHANGED"]);
        }

        LessonVideo lessonVideo;
        BunnyVideoAsset asset;
        try
        {
            lessonVideo = replacementTarget.LessonVideo ?? new LessonVideo
            {
                Title = title,
                Provider = VideoProviders.Bunny,
                ProviderVideoId = bunnyVideo.Guid,
                Order = request.Order,
                MaxWatchCount = request.MaxWatchCount,
                LessonId = request.LessonId,
                VideoTypeId = request.VideoTypeId,
                IsActive = false,
                BunnyStreamLibraryId = libraryResult.Access.Id
            };
            if (!replacementTarget.IsReplacement)
            {
                _db.LessonVideos.Add(lessonVideo);
            }

            asset = new BunnyVideoAsset
            {
                LessonVideo = lessonVideo,
                TeacherId = ownership.TeacherId,
                PackageId = ownership.PackageId,
                LessonId = request.LessonId,
                UploadedByUserId = request.CurrentUserId,
                BunnyLibraryId = bunnyVideo.VideoLibraryId,
                BunnyVideoGuid = bunnyVideo.Guid,
                Title = title,
                UploadMethod = "UrlFetch",
                Status = "Processing",
                SourceUrlHash = Sha256(request.SourceUrl),
                BunnyEncodeProgress = bunnyVideo.EncodeProgress,
                ActivateWhenReady = request.IsActive,
                SourceState = replacementTarget.IsReplacement
                    ? BunnyVideoAssetSourceState.PendingReplacement
                    : BunnyVideoAssetSourceState.Current,
                BunnyStreamLibraryRecordId = libraryResult.Access.Id,
                TargetOrder = replacementTarget.IsReplacement ? request.Order : null,
                TargetMaxWatchCount = replacementTarget.IsReplacement ? request.MaxWatchCount : null,
                TargetVideoTypeId = replacementTarget.IsReplacement ? request.VideoTypeId : null,
                TargetIsActive = replacementTarget.IsReplacement ? request.IsActive : null,
                TargetSourceRevision = replacementTarget.IsReplacement
                    ? replacementTarget.ExpectedSourceRevision
                    : null
            };
            _db.BunnyVideoAssets.Add(asset);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Local persistence failed after starting a Bunny URL fetch in library {BunnyLibraryId}; remote cleanup was requested. Failure type: {FailureType}.",
                libraryResult.Access.ExternalLibraryId,
                exception.GetType().Name);
            await BunnyRemoteVideoCompensation.DeleteBestEffortAsync(
                bunny,
                libraryResult.Access.ExternalLibraryId,
                bunnyVideo.Guid,
                "URL fetch persistence",
                _logger);
            throw;
        }

        return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto(fetchResult.Message));
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

public sealed class RefreshBunnyVideoStatusCommandHandler : IRequestHandler<RefreshBunnyVideoStatusCommand, ApiResponse<BunnyUploadStatusDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _libraries;
    private readonly IBunnyStreamClientFactory _clients;

    public RefreshBunnyVideoStatusCommandHandler(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients)
    {
        _db = db;
        _libraries = libraries;
        _clients = clients;
    }

    public async Task<ApiResponse<BunnyUploadStatusDto>> Handle(RefreshBunnyVideoStatusCommand request, CancellationToken cancellationToken)
    {
        var asset = await _db.BunnyVideoAssets.Include(a => a.LessonVideo).FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);
        if (asset is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Bunny asset not found.");
        }

        if (asset.SourceState == BunnyVideoAssetSourceState.Retired)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                "تمت أرشفة أصل Bunny هذا ولا يمكن تحديثه.",
                ["BUNNY_ASSET_RETIRED"]);
        }

        var canAccess = await BunnyUploadAuthorization.CanAccessAssetAsync(_db, request.CurrentUserId, asset, cancellationToken);
        if (!canAccess)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Unauthorized access to this Bunny video.");
        }

        if (BunnyVideoReplacementLifecycle.ExpireIfNeeded(asset, DateTime.UtcNow))
        {
            if (!await BunnyVideoReplacementLifecycle.TrySaveAssetStateAsync(_db, asset, cancellationToken))
            {
                return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto(null));
            }

            return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto("انتهت مهلة الاستبدال قبل اكتمال Bunny."));
        }

        var bunnyResult = await BunnyUploadClientResolver.ResolveAsync(_libraries, _clients, asset, cancellationToken);
        if (!bunnyResult.Success || bunnyResult.Client is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(
                bunnyResult.Message ?? "تعذر الوصول إلى مكتبة Bunny المرتبطة بالفيديو.",
                [bunnyResult.ErrorCode ?? "BUNNY_LIBRARY_UNAVAILABLE"]);
        }

        await BunnyUploadStatusUpdater.RefreshAsync(bunnyResult.Client, asset, cancellationToken);
        var replacementWasApplied = await BunnyVideoReplacementLifecycle.FinalizeIfNeededAsync(
            _db,
            asset,
            cancellationToken);
        if (!replacementWasApplied)
        {
            await BunnyVideoReplacementLifecycle.TrySaveAssetStateAsync(_db, asset, cancellationToken);
        }
        return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto(null));
    }
}

public sealed class RefreshPendingBunnyVideosCommandHandler
    : IRequestHandler<RefreshPendingBunnyVideosCommand, BunnyPendingRefreshResultDto>
{
    private static readonly string[] PendingStatuses = ["Created", "Uploaded", "Processing", "Transcoding", "Unknown"];
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _libraries;
    private readonly IBunnyStreamClientFactory _clients;

    public RefreshPendingBunnyVideosCommandHandler(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients)
    {
        _db = db;
        _libraries = libraries;
        _clients = clients;
    }

    public async Task<BunnyPendingRefreshResultDto> Handle(
        RefreshPendingBunnyVideosCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await BunnyVideoReplacementLifecycle.ExpirePendingReplacementsAsync(_db, now, cancellationToken);

        var batchSize = Math.Clamp(request.BatchSize, 1, 100);
        var staleBefore = now - RefreshInterval;
        var missingVideoRetryCutoff = now - BunnyUploadStatusUpdater.MissingVideoRetryWindow;
        var candidateIds = await _db.BunnyVideoAssets
            .AsNoTracking()
            .Where(asset => PendingStatuses.Contains(asset.Status)
                && asset.SourceState != BunnyVideoAssetSourceState.Retired
                && (asset.Status != "Unknown" || asset.CreatedAt >= missingVideoRetryCutoff)
                && (asset.LastStatusSyncedAtUtc == null || asset.LastStatusSyncedAtUtc < staleBefore))
            .OrderBy(asset => asset.LastStatusSyncedAtUtc)
            .ThenBy(asset => asset.CreatedAt)
            .Select(asset => asset.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var attempted = 0;
        var refreshed = 0;
        var failed = 0;

        foreach (var assetId in candidateIds)
        {
            var claimedAt = DateTime.UtcNow;
            var claimed = await _db.BunnyVideoAssets
                .Where(asset => asset.Id == assetId
                    && PendingStatuses.Contains(asset.Status)
                    && asset.SourceState != BunnyVideoAssetSourceState.Retired
                    && (asset.Status != "Unknown" || asset.CreatedAt >= missingVideoRetryCutoff)
                    && (asset.LastStatusSyncedAtUtc == null || asset.LastStatusSyncedAtUtc < staleBefore))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(asset => asset.LastStatusSyncedAtUtc, claimedAt), cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            attempted++;
            var asset = await _db.BunnyVideoAssets
                .Include(item => item.LessonVideo)
                .FirstOrDefaultAsync(item => item.Id == assetId, cancellationToken);
            if (asset is null)
            {
                failed++;
                continue;
            }

            var bunnyResult = await BunnyUploadClientResolver.ResolveAsync(
                _libraries,
                _clients,
                asset,
                cancellationToken);
            if (!bunnyResult.Success || bunnyResult.Client is null)
            {
                failed++;
                continue;
            }

            try
            {
                await BunnyUploadStatusUpdater.RefreshAsync(
                    bunnyResult.Client,
                    asset,
                    cancellationToken);
                var replacementWasApplied = await BunnyVideoReplacementLifecycle.FinalizeIfNeededAsync(
                    _db,
                    asset,
                    cancellationToken);
                if (!replacementWasApplied)
                {
                    await BunnyVideoReplacementLifecycle.TrySaveAssetStateAsync(
                        _db,
                        asset,
                        cancellationToken);
                }

                refreshed++;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failed++;
            }
            catch (Exception exception) when (
                exception is HttpRequestException
                    or InvalidOperationException
                    or System.Text.Json.JsonException)
            {
                failed++;
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Every asset was persisted at the end of its own refresh iteration;
            // discard only a stale tracker snapshot left by a concurrent cancel.
            _db.ClearTrackedChanges();
        }
        return new BunnyPendingRefreshResultDto(attempted, refreshed, failed);
    }
}

internal static class BunnyUploadAuthorization
{
    public static async Task<(bool Success, string Message, Guid TeacherId, Guid PackageId)> ResolveAsync(
        IAppDbContext db,
        Guid currentUserId,
        Guid? requestedTeacherId,
        Guid? packageId,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);

        if (user is null)
        {
            return (false, "User not found.", Guid.Empty, Guid.Empty);
        }

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin || ur.Role.Name == "Admin");
        var isTeacher = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher || ur.Role.Name == "Teacher");

        // Fetch lesson ownership first so we can auto-resolve teacher/package
        var lessonOwnership = await db.Lessons
            .Where(l => l.Id == lessonId)
            .Select(l => new
            {
                PackageId = l.ContentSection.Term.PackageId,
                TeacherId = l.ContentSection.Term.Package.TeacherId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (lessonOwnership is null)
        {
            return (false, "Lesson not found.", Guid.Empty, Guid.Empty);
        }

        // Resolve teacher: for admins, default to lesson owner if not explicitly provided
        var teacherId = isAdmin
            ? (requestedTeacherId.HasValue && requestedTeacherId.Value != Guid.Empty
                ? requestedTeacherId.Value
                : lessonOwnership.TeacherId)
            : user.TeacherProfile?.Id ?? Guid.Empty;

        if (teacherId == Guid.Empty)
        {
            return (false, "Teacher is required for Bunny upload.", Guid.Empty, Guid.Empty);
        }

        // Resolve package: default to lesson's package if not explicitly provided
        var resolvedPackageId = packageId.HasValue && packageId.Value != Guid.Empty
            ? packageId.Value
            : lessonOwnership.PackageId;

        if (resolvedPackageId != lessonOwnership.PackageId || lessonOwnership.TeacherId != teacherId)
        {
            return (false, "Selected teacher, package, and lesson do not match.", Guid.Empty, Guid.Empty);
        }

        if (!isAdmin && !isTeacher)
        {
            return (false, "Unauthorized Bunny upload role.", Guid.Empty, Guid.Empty);
        }

        return (true, string.Empty, teacherId, resolvedPackageId);
    }

    public static async Task<bool> CanAccessAssetAsync(IAppDbContext db, Guid currentUserId, BunnyVideoAsset asset, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);

        if (user is null)
        {
            return false;
        }

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin || ur.Role.Name == "Admin");
        return isAdmin || user.TeacherProfile?.Id == asset.TeacherId;
    }
}

internal static class BunnyUploadStatusUpdater
{
    internal static readonly TimeSpan MissingVideoRetryWindow = TimeSpan.FromMinutes(15);

    public static async Task RefreshAsync(IBunnyStreamClient bunny, BunnyVideoAsset asset, CancellationToken cancellationToken)
    {
        var video = await bunny.GetVideoAsync(asset.BunnyVideoGuid, cancellationToken);
        var now = DateTime.UtcNow;
        asset.LastStatusSyncedAtUtc = now;
        if (video is null)
        {
            if (asset.SourceState == BunnyVideoAssetSourceState.Current)
            {
                asset.LessonVideo.IsActive = false;
            }
            if (asset.CreatedAt >= now - MissingVideoRetryWindow)
            {
                asset.Status = "Processing";
                asset.ErrorMessage = "Bunny has not returned this newly created video yet.";
            }
            else
            {
                asset.Status = "Unknown";
                asset.ErrorMessage = "Bunny did not return this video.";
            }
            return;
        }

        asset.BunnyEncodeProgress = video.EncodeProgress;
        asset.StorageBytes = video.StorageSize;
        asset.DurationSeconds = video.Length;
        asset.Status = BunnyVideoStatusClassifier.Classify(video.Status) switch
        {
            BunnyVideoLifecycleState.Processing => "Processing",
            BunnyVideoLifecycleState.Ready => "Ready",
            BunnyVideoLifecycleState.Failed => "Failed",
            _ => "Unknown"
        };

        if (asset.Status == "Ready")
        {
            asset.ErrorMessage = null;
            if (asset.SourceState == BunnyVideoAssetSourceState.Current)
            {
                asset.LessonVideo.IsActive = asset.ActivateWhenReady;
            }
        }
        else if (asset.Status is "Failed" or "Unknown")
        {
            if (asset.SourceState == BunnyVideoAssetSourceState.Current)
            {
                asset.LessonVideo.IsActive = false;
            }
            asset.ErrorMessage = asset.Status == "Failed"
                ? "Bunny failed to process this video."
                : "Bunny did not return this video.";
        }
        else
        {
            if (asset.SourceState == BunnyVideoAssetSourceState.Current)
            {
                asset.LessonVideo.IsActive = false;
            }
        }
    }

    public static BunnyUploadStatusDto ToStatusDto(this BunnyVideoAsset asset, string? message)
    {
        return new BunnyUploadStatusDto(asset.Id, asset.LessonVideoId, asset.Status, asset.BunnyEncodeProgress, message);
    }
}

internal static class BunnyVideoReplacementLifecycle
{
    internal static readonly TimeSpan PendingReplacementExpiry = TimeSpan.FromHours(24);

    /// <summary>
    /// A cancellation/expiry can legitimately win while a status refresh is in
    /// flight. SourceState is a concurrency token, so treat that as a benign
    /// terminal outcome and reload the asset instead of surfacing a 500 response.
    /// </summary>
    public static async Task<bool> TrySaveAssetStateAsync(
        IAppDbContext db,
        BunnyVideoAsset asset,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(asset).ReloadAsync(cancellationToken);
            return false;
        }
    }

    public static async Task<int> ExpirePendingReplacementsAsync(
        IAppDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var expiredCandidates = await db.BunnyVideoAssets
            .Where(asset => asset.SourceState == BunnyVideoAssetSourceState.PendingReplacement
                && asset.CreatedAt < now - PendingReplacementExpiry)
            .ToListAsync(cancellationToken);
        foreach (var candidate in expiredCandidates)
        {
            Expire(candidate, now);
        }

        if (expiredCandidates.Count > 0)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A refresher/cancel may already have moved one candidate. Do not
                // overwrite it; the next polling pass will re-evaluate the rest.
                db.ClearTrackedChanges();
                return 0;
            }
        }

        return expiredCandidates.Count;
    }

    public static bool ExpirePendingReplacements(
        IEnumerable<BunnyVideoAsset> assets,
        DateTime now)
    {
        var expiredAny = false;
        foreach (var asset in assets)
        {
            expiredAny |= ExpireIfNeeded(asset, now);
        }

        return expiredAny;
    }

    public static bool ExpireIfNeeded(BunnyVideoAsset asset, DateTime now)
    {
        if (asset.SourceState != BunnyVideoAssetSourceState.PendingReplacement
            || asset.CreatedAt >= now - PendingReplacementExpiry)
        {
            return false;
        }

        Expire(asset, now);
        return true;
    }

    public static void Cancel(BunnyVideoAsset asset, Guid cancelledByUserId)
    {
        asset.Status = "Cancelled";
        asset.ErrorMessage = "Bunny replacement was cancelled before becoming ready.";
        LessonVideoSourceMutation.RetireBunnyAsset(asset, cancelledByUserId);
    }

    private static void Expire(BunnyVideoAsset asset, DateTime now)
    {
        asset.Status = "Expired";
        asset.ErrorMessage = "Bunny replacement expired before becoming ready.";
        LessonVideoSourceMutation.RetireBunnyAsset(asset, null);
        asset.RetiredAtUtc = now;
        asset.UpdatedAt = now;
    }

    public static async Task<bool> FinalizeIfNeededAsync(
        IAppDbContext db,
        BunnyVideoAsset candidate,
        CancellationToken cancellationToken)
    {
        if (candidate.SourceState != BunnyVideoAssetSourceState.PendingReplacement)
        {
            return false;
        }

        var unknownIsTerminal = string.Equals(candidate.Status, "Unknown", StringComparison.OrdinalIgnoreCase)
            && candidate.CreatedAt < DateTime.UtcNow - BunnyUploadStatusUpdater.MissingVideoRetryWindow;
        if (string.Equals(candidate.Status, "Failed", StringComparison.OrdinalIgnoreCase) || unknownIsTerminal)
        {
            LessonVideoSourceMutation.RetireBunnyAsset(candidate, null);
            return false;
        }

        if (!string.Equals(candidate.Status, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!candidate.BunnyStreamLibraryRecordId.HasValue)
        {
            candidate.Status = "Failed";
            candidate.ErrorMessage = "Bunny asset library metadata is missing.";
            LessonVideoSourceMutation.RetireBunnyAsset(candidate, null);
            return false;
        }

        if (!candidate.TargetSourceRevision.HasValue)
        {
            candidate.Status = "Failed";
            candidate.ErrorMessage = "Bunny replacement source revision metadata is missing.";
            LessonVideoSourceMutation.RetireBunnyAsset(candidate, null);
            return false;
        }

        // Persist the status refresh while SourceState is still PendingReplacement.
        // SourceState is a concurrency token, so a completed cancellation/expiry wins
        // instead of a stale refresher being able to promote the candidate later.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(candidate).ReloadAsync(cancellationToken);
            return false;
        }

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var canStillPromote = await db.BunnyVideoAssets
            .AsNoTracking()
            .AnyAsync(
                asset => asset.Id == candidate.Id
                    && asset.SourceState == BunnyVideoAssetSourceState.PendingReplacement
                    && asset.Status == "Ready",
                cancellationToken);
        if (!canStillPromote)
        {
            await transaction.RollbackAsync(cancellationToken);
            await db.Entry(candidate).ReloadAsync(cancellationToken);
            return false;
        }

        var target = candidate.LessonVideo;
        if (target is null)
        {
            target = await db.LessonVideos
                .FirstOrDefaultAsync(video => video.Id == candidate.LessonVideoId, cancellationToken)
                ?? throw new InvalidOperationException("The Bunny replacement target no longer exists.");
        }

        await db.Entry(target).ReloadAsync(cancellationToken);
        var expectedSourceRevision = candidate.TargetSourceRevision.Value;
        if (target.SourceRevision != expectedSourceRevision)
        {
            var supersededAtUtc = DateTime.UtcNow;
            var superseded = await db.BunnyVideoAssets
                .Where(asset => asset.Id == candidate.Id
                    && asset.SourceState == BunnyVideoAssetSourceState.PendingReplacement)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(asset => asset.Status, "Failed")
                    .SetProperty(asset => asset.ErrorMessage, "Bunny replacement was superseded by a newer video source edit.")
                    .SetProperty(asset => asset.SourceState, BunnyVideoAssetSourceState.Retired)
                    .SetProperty(asset => asset.RetiredAtUtc, supersededAtUtc)
                    .SetProperty(asset => asset.RetiredByUserId, (Guid?)null)
                    .SetProperty(asset => asset.ActivateWhenReady, false)
                    .SetProperty(asset => asset.OutcomeSupersededAtUtc, supersededAtUtc)
                    .SetProperty(asset => asset.UpdatedAt, supersededAtUtc), cancellationToken);
            if (superseded == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            else
            {
                await transaction.CommitAsync(cancellationToken);
            }

            await db.Entry(candidate).ReloadAsync(cancellationToken);
            return false;
        }

        var targetOrder = candidate.TargetOrder ?? target.Order;
        var targetMaxWatchCount = candidate.TargetMaxWatchCount ?? target.MaxWatchCount;
        var targetVideoTypeId = candidate.TargetVideoTypeId ?? target.VideoTypeId;
        var targetIsActive = candidate.TargetIsActive ?? false;

        var now = DateTime.UtcNow;
        // Avoid a transient filtered-index collision and, crucially, use a compare-
        // and-set promotion so a cancellation that won the race cannot be resurrected.
        await db.BunnyVideoAssets
            .Where(asset => asset.LessonVideoId == candidate.LessonVideoId
                && asset.SourceState == BunnyVideoAssetSourceState.Current)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(asset => asset.SourceState, BunnyVideoAssetSourceState.Retired)
                .SetProperty(asset => asset.RetiredAtUtc, now)
                .SetProperty(asset => asset.RetiredByUserId, (Guid?)null)
                .SetProperty(asset => asset.ActivateWhenReady, false)
                .SetProperty(asset => asset.UpdatedAt, now), cancellationToken);

        var promoted = await db.BunnyVideoAssets
            .Where(asset => asset.Id == candidate.Id
                && asset.SourceState == BunnyVideoAssetSourceState.PendingReplacement
                && asset.Status == "Ready")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(asset => asset.SourceState, BunnyVideoAssetSourceState.Current)
                .SetProperty(asset => asset.RetiredAtUtc, (DateTime?)null)
                .SetProperty(asset => asset.RetiredByUserId, (Guid?)null)
                .SetProperty(asset => asset.ActivateWhenReady, targetIsActive)
                .SetProperty(asset => asset.TargetOrder, (int?)null)
                .SetProperty(asset => asset.TargetMaxWatchCount, (int?)null)
                .SetProperty(asset => asset.TargetVideoTypeId, (Guid?)null)
                .SetProperty(asset => asset.TargetIsActive, (bool?)null)
                .SetProperty(asset => asset.TargetSourceRevision, (int?)null)
                .SetProperty(asset => asset.UpdatedAt, now), cancellationToken);
        if (promoted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            await db.Entry(candidate).ReloadAsync(cancellationToken);
            return false;
        }

        await db.Entry(candidate).ReloadAsync(cancellationToken);

        var historicalOutcomes = await db.BunnyVideoAssets
            .Where(asset => asset.LessonVideoId == candidate.LessonVideoId
                && asset.SourceState == BunnyVideoAssetSourceState.Retired
                && asset.OutcomeSupersededAtUtc == null
                && (asset.Status == "Failed"
                    || asset.Status == "Expired"
                    || asset.Status == "Cancelled"
                    || asset.Status == "Unknown"))
            .ToListAsync(cancellationToken);
        LessonVideoSourceMutation.SuppressHistoricalBunnyReplacementOutcomes(
            historicalOutcomes,
            now);

        await LessonVideoSourceMutation.InvalidateSourceDerivedDataAsync(db, target, cancellationToken);

        target.Title = candidate.Title;
        target.Provider = VideoProviders.Bunny;
        target.ProviderVideoId = candidate.BunnyVideoGuid;
        target.BunnyStreamLibraryId = candidate.BunnyStreamLibraryRecordId.Value;
        target.Order = targetOrder;
        target.MaxWatchCount = targetMaxWatchCount;
        target.VideoTypeId = targetVideoTypeId;
        target.IsActive = targetIsActive;
        target.UpdatedAt = now;
        checked
        {
            target.SourceRevision = expectedSourceRevision + 1;
        }

        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "VideoUpdated",
            TargetGroup = $"Lesson_{target.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = target.LessonId,
                videoId = target.Id,
                title = target.Title
            })
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ClearTrackedChanges();
            db.BunnyVideoAssets.Attach(candidate);
            await db.Entry(candidate).ReloadAsync(cancellationToken);
            return false;
        }
    }
}

internal static class BunnyRemoteVideoCompensation
{
    public static async Task DeleteBestEffortAsync(
        IBunnyStreamClient bunny,
        long externalLibraryId,
        string videoGuid,
        string operation,
        ILogger logger)
    {
        try
        {
            await bunny.DeleteVideoAsync(videoGuid, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Bunny remote cleanup failed after {BunnyOperation} in library {BunnyLibraryId}; manual cleanup may be required. Failure type: {FailureType}.",
                operation,
                externalLibraryId,
                exception.GetType().Name);
        }
    }
}

internal sealed record BunnyUploadClientResolution(
    bool Success,
    IBunnyStreamClient? Client,
    string? ErrorCode,
    string? Message);

internal static class BunnyUploadClientResolver
{
    public static async Task<BunnyUploadClientResolution> ResolveAsync(
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients,
        BunnyVideoAsset asset,
        CancellationToken cancellationToken)
    {
        BunnyStreamLibraryAccessResult access;
        if (asset.BunnyStreamLibraryRecordId.HasValue)
        {
            access = await libraries.ResolveAsync(
                asset.BunnyStreamLibraryRecordId.Value,
                requireActive: false,
                cancellationToken);
        }
        else if (asset.LessonVideo.BunnyStreamLibraryId.HasValue)
        {
            access = await libraries.ResolveAsync(
                asset.LessonVideo.BunnyStreamLibraryId.Value,
                requireActive: false,
                cancellationToken);
        }
        else
        {
            access = await libraries.ResolveByExternalIdAsync(
                asset.BunnyLibraryId,
                requireActive: false,
                cancellationToken);
        }

        return access.Success && access.Access is not null
            ? new BunnyUploadClientResolution(
                true,
                clients.Create(access.Access.ExternalLibraryId, access.Access.ApiKey),
                null,
                null)
            : new BunnyUploadClientResolution(false, null, access.ErrorCode, access.Message);
    }
}
