using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
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
    string? FileName,
    long? FileSizeBytes,
    Guid CurrentUserId) : IRequest<ApiResponse<BunnyTusUploadSessionDto>>;

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

public record FetchBunnyVideoCommand(
    Guid? TeacherId,
    Guid? PackageId,
    Guid LessonId,
    string Title,
    int Order,
    int MaxWatchCount,
    string SourceUrl,
    Guid CurrentUserId) : IRequest<ApiResponse<BunnyUploadStatusDto>>;

public record RefreshBunnyVideoStatusCommand(Guid AssetId, Guid CurrentUserId) : IRequest<ApiResponse<BunnyUploadStatusDto>>;

public record BunnyUploadStatusDto(Guid AssetId, Guid LessonVideoId, string Status, int? EncodeProgress, string? Message);

public sealed class CreateBunnyTusUploadCommandHandler : IRequestHandler<CreateBunnyTusUploadCommand, ApiResponse<BunnyTusUploadSessionDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamClient _bunny;
    private readonly IConfiguration _configuration;

    public CreateBunnyTusUploadCommandHandler(IAppDbContext db, IBunnyStreamClient bunny, IConfiguration configuration)
    {
        _db = db;
        _bunny = bunny;
        _configuration = configuration;
    }

    public async Task<ApiResponse<BunnyTusUploadSessionDto>> Handle(CreateBunnyTusUploadCommand request, CancellationToken cancellationToken)
    {
        var ownership = await BunnyUploadAuthorization.ResolveAsync(_db, request.CurrentUserId, request.TeacherId, request.PackageId, request.LessonId, cancellationToken);
        if (!ownership.Success)
        {
            return ApiResponse<BunnyTusUploadSessionDto>.Fail(ownership.Message);
        }

        var bunnyVideo = await _bunny.CreateVideoAsync(request.Title.Trim(), collectionId: null, cancellationToken);
        var lessonVideo = new LessonVideo
        {
            Title = request.Title.Trim(),
            Provider = VideoProviders.Bunny,
            ProviderVideoId = bunnyVideo.Guid,
            Order = request.Order,
            MaxWatchCount = request.MaxWatchCount,
            LessonId = request.LessonId,
            IsActive = true
        };
        _db.LessonVideos.Add(lessonVideo);

        var asset = new BunnyVideoAsset
        {
            LessonVideo = lessonVideo,
            TeacherId = ownership.TeacherId,
            PackageId = ownership.PackageId,
            LessonId = request.LessonId,
            UploadedByUserId = request.CurrentUserId,
            BunnyLibraryId = bunnyVideo.VideoLibraryId,
            BunnyVideoGuid = bunnyVideo.Guid,
            Title = request.Title.Trim(),
            UploadMethod = "TusFile",
            Status = "Created",
            OriginalFileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            BunnyEncodeProgress = bunnyVideo.EncodeProgress,
            StorageBytes = bunnyVideo.StorageSize,
            DurationSeconds = bunnyVideo.Length
        };
        _db.BunnyVideoAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        var expiryMinutes = int.TryParse(_configuration["BunnyStream:TusUploadExpiryMinutes"], out var parsed) ? parsed : 180;
        var signature = _bunny.CreateTusUploadSignature(bunnyVideo.Guid, TimeSpan.FromMinutes(expiryMinutes));
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
    private readonly IBunnyStreamClient _bunny;

    public CompleteBunnyUploadCommandHandler(IAppDbContext db, IBunnyStreamClient bunny)
    {
        _db = db;
        _bunny = bunny;
    }

    public async Task<ApiResponse<BunnyUploadStatusDto>> Handle(CompleteBunnyUploadCommand request, CancellationToken cancellationToken)
    {
        var asset = await _db.BunnyVideoAssets.Include(a => a.LessonVideo).FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);
        if (asset is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Bunny asset not found.");
        }

        var canAccess = await BunnyUploadAuthorization.CanAccessAssetAsync(_db, request.CurrentUserId, asset, cancellationToken);
        if (!canAccess)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Unauthorized access to this Bunny upload.");
        }

        await BunnyUploadStatusUpdater.RefreshAsync(_bunny, asset, cancellationToken);
        if (asset.Status == "Created" || asset.Status == "Uploading")
        {
            asset.Status = "Uploaded";
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto(null));
    }
}

public sealed class FetchBunnyVideoCommandHandler : IRequestHandler<FetchBunnyVideoCommand, ApiResponse<BunnyUploadStatusDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamClient _bunny;

    public FetchBunnyVideoCommandHandler(IAppDbContext db, IBunnyStreamClient bunny)
    {
        _db = db;
        _bunny = bunny;
    }

    public async Task<ApiResponse<BunnyUploadStatusDto>> Handle(FetchBunnyVideoCommand request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Remote video URL must be a valid HTTP/HTTPS URL.");
        }

        var ownership = await BunnyUploadAuthorization.ResolveAsync(_db, request.CurrentUserId, request.TeacherId, request.PackageId, request.LessonId, cancellationToken);
        if (!ownership.Success)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(ownership.Message);
        }

        var bunnyVideo = await _bunny.CreateVideoAsync(request.Title.Trim(), collectionId: null, cancellationToken);
        var fetchResult = await _bunny.FetchVideoAsync(request.SourceUrl, request.Title.Trim(), collectionId: null, cancellationToken);
        if (!fetchResult.Success)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail(fetchResult.Message ?? "Bunny fetch request failed.");
        }

        var lessonVideo = new LessonVideo
        {
            Title = request.Title.Trim(),
            Provider = VideoProviders.Bunny,
            ProviderVideoId = bunnyVideo.Guid,
            Order = request.Order,
            MaxWatchCount = request.MaxWatchCount,
            LessonId = request.LessonId,
            IsActive = false
        };
        _db.LessonVideos.Add(lessonVideo);

        var asset = new BunnyVideoAsset
        {
            LessonVideo = lessonVideo,
            TeacherId = ownership.TeacherId,
            PackageId = ownership.PackageId,
            LessonId = request.LessonId,
            UploadedByUserId = request.CurrentUserId,
            BunnyLibraryId = bunnyVideo.VideoLibraryId,
            BunnyVideoGuid = bunnyVideo.Guid,
            Title = request.Title.Trim(),
            UploadMethod = "UrlFetch",
            Status = "Processing",
            SourceUrlHash = Sha256(request.SourceUrl),
            BunnyEncodeProgress = bunnyVideo.EncodeProgress
        };
        _db.BunnyVideoAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

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
    private readonly IBunnyStreamClient _bunny;

    public RefreshBunnyVideoStatusCommandHandler(IAppDbContext db, IBunnyStreamClient bunny)
    {
        _db = db;
        _bunny = bunny;
    }

    public async Task<ApiResponse<BunnyUploadStatusDto>> Handle(RefreshBunnyVideoStatusCommand request, CancellationToken cancellationToken)
    {
        var asset = await _db.BunnyVideoAssets.Include(a => a.LessonVideo).FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);
        if (asset is null)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Bunny asset not found.");
        }

        var canAccess = await BunnyUploadAuthorization.CanAccessAssetAsync(_db, request.CurrentUserId, asset, cancellationToken);
        if (!canAccess)
        {
            return ApiResponse<BunnyUploadStatusDto>.Fail("Unauthorized access to this Bunny video.");
        }

        await BunnyUploadStatusUpdater.RefreshAsync(_bunny, asset, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return ApiResponse<BunnyUploadStatusDto>.Ok(asset.ToStatusDto(null));
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
        var teacherId = isAdmin ? requestedTeacherId.GetValueOrDefault() : user.TeacherProfile?.Id ?? Guid.Empty;

        if (teacherId == Guid.Empty)
        {
            return (false, "Teacher is required for Bunny upload.", Guid.Empty, Guid.Empty);
        }

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

        if ((packageId.HasValue && lessonOwnership.PackageId != packageId.Value) || lessonOwnership.TeacherId != teacherId)
        {
            return (false, "Selected teacher, package, and lesson do not match.", Guid.Empty, Guid.Empty);
        }

        if (!isAdmin && !isTeacher)
        {
            return (false, "Unauthorized Bunny upload role.", Guid.Empty, Guid.Empty);
        }

        return (true, string.Empty, teacherId, lessonOwnership.PackageId);
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
    public static async Task RefreshAsync(IBunnyStreamClient bunny, BunnyVideoAsset asset, CancellationToken cancellationToken)
    {
        var video = await bunny.GetVideoAsync(asset.BunnyVideoGuid, cancellationToken);
        asset.LastStatusSyncedAtUtc = DateTime.UtcNow;
        if (video is null)
        {
            asset.Status = "Unknown";
            return;
        }

        asset.BunnyEncodeProgress = video.EncodeProgress;
        asset.StorageBytes = video.StorageSize;
        asset.DurationSeconds = video.Length;
        asset.Status = video.Status switch
        {
            0 => "Created",
            1 => "Uploaded",
            2 => "Processing",
            3 => "Processing",
            4 => "Ready",
            5 => "Failed",
            6 => "Failed",
            _ => "Processing"
        };
    }

    public static BunnyUploadStatusDto ToStatusDto(this BunnyVideoAsset asset, string? message)
    {
        return new BunnyUploadStatusDto(asset.Id, asset.LessonVideoId, asset.Status, asset.BunnyEncodeProgress, message);
    }
}
