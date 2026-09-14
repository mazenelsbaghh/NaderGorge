using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record SyncBunnyUsageCommand(DateTime PeriodStart, DateTime PeriodEnd, Guid? TeacherId, Guid? PackageId, bool ForceRefresh, Guid CurrentUserId)
    : IRequest<ApiResponse<BunnyUsageSyncResultDto>>;

public record BunnyUsageSyncResultDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int SnapshotsCreated,
    int SnapshotsUpdated,
    int EstimatedBandwidthCount,
    decimal StorageRateUsdPerGb,
    decimal BandwidthRateUsdPerGb,
    int FailedLibraryCount = 0);

public sealed class SyncBunnyUsageCommandHandler : IRequestHandler<SyncBunnyUsageCommand, ApiResponse<BunnyUsageSyncResultDto>>
{
    private const decimal BytesPerGb = 1024m * 1024m * 1024m;

    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _libraries;
    private readonly IBunnyStreamClientFactory _clients;
    private readonly ICachedPlatformSettingsReader _settingsReader;

    public SyncBunnyUsageCommandHandler(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService libraries,
        IBunnyStreamClientFactory clients,
        ICachedPlatformSettingsReader settingsReader)
    {
        _db = db;
        _libraries = libraries;
        _clients = clients;
        _settingsReader = settingsReader;
    }

    public async Task<ApiResponse<BunnyUsageSyncResultDto>> Handle(SyncBunnyUsageCommand request, CancellationToken cancellationToken)
    {
        if (request.PeriodEnd <= request.PeriodStart)
        {
            return ApiResponse<BunnyUsageSyncResultDto>.Fail("Period end must be after period start.");
        }

        var periodStart = DateTime.SpecifyKind(request.PeriodStart.Date, DateTimeKind.Utc);
        var periodEnd = DateTime.SpecifyKind(request.PeriodEnd.Date, DateTimeKind.Utc);
        var settings = await _settingsReader.GetAsync(cancellationToken);

        var assetsQuery = _db.BunnyVideoAssets.AsQueryable();
        if (request.TeacherId.HasValue)
        {
            assetsQuery = assetsQuery.Where(asset => asset.TeacherId == request.TeacherId.Value);
        }
        if (request.PackageId.HasValue)
        {
            assetsQuery = assetsQuery.Where(asset => asset.PackageId == request.PackageId.Value);
        }

        var assets = await assetsQuery.ToListAsync(cancellationToken);
        if (assets.Count == 0)
        {
            return ApiResponse<BunnyUsageSyncResultDto>.Ok(new BunnyUsageSyncResultDto(periodStart, periodEnd, 0, 0, 0, settings.BunnyStreamStorageRateUsdPerGb, settings.BunnyStreamBandwidthRateUsdPerGb));
        }

        var created = 0;
        var updated = 0;
        var estimated = 0;
        var failedLibraries = 0;

        foreach (var libraryAssets in assets.GroupBy(asset => asset.BunnyLibraryId))
        {
            var access = await _libraries.ResolveByExternalIdAsync(
                libraryAssets.Key,
                requireActive: false,
                cancellationToken);
            if (!access.Success || access.Access is null)
            {
                failedLibraries++;
                continue;
            }

            var bunny = _clients.Create(access.Access.ExternalLibraryId, access.Access.ApiKey);
            BunnyVideoLibraryDto? library;
            IReadOnlyList<BunnyStreamVideoDto> videos;
            try
            {
                library = await bunny.GetVideoLibraryAsync(cancellationToken);
                videos = await bunny.ListVideosAsync(cancellationToken);
            }
            catch (HttpRequestException)
            {
                failedLibraries++;
                continue;
            }

            // Stream library keys do not necessarily authorize Bunny's account-level
            // video-library endpoint. Storage can still be refreshed, but existing
            // bandwidth evidence must never be replaced with zero in that case.
            var libraryRefreshIncomplete = library is null;

            var totalWatchTime = Math.Max(1, videos.Sum(video => video.TotalWatchTime));
            var trafficUsage = library?.TrafficUsage ?? 0;

            foreach (var asset in libraryAssets)
            {
                var snapshot = await _db.BunnyUsageSnapshots.FirstOrDefaultAsync(s =>
                    s.BunnyVideoAssetId == asset.Id &&
                    s.PeriodStartUtc == periodStart &&
                    s.PeriodEndUtc == periodEnd,
                    cancellationToken);

                BunnyStreamVideoDto? bunnyVideo;
                BunnyVideoStorageDto? storageInfo;
                try
                {
                    bunnyVideo = videos.FirstOrDefault(video => string.Equals(video.Guid, asset.BunnyVideoGuid, StringComparison.OrdinalIgnoreCase))
                        ?? await bunny.GetVideoAsync(asset.BunnyVideoGuid, cancellationToken);
                    storageInfo = await bunny.GetVideoStorageAsync(asset.BunnyVideoGuid, cancellationToken);
                }
                catch (HttpRequestException)
                {
                    // One unavailable video must not abort usage refresh for the other
                    // videos or libraries. Existing evidence remains untouched.
                    libraryRefreshIncomplete = true;
                    continue;
                }

                if (snapshot is not null && bunnyVideo is null && storageInfo is null)
                {
                    continue;
                }

                var storageBytes = storageInfo?.TotalBytes ?? bunnyVideo?.StorageSize ?? asset.StorageBytes ?? 0;
                var canEstimateBandwidth = library is not null && bunnyVideo is not null;
                var bandwidthBytes = canEstimateBandwidth
                    ? (long)Math.Round(trafficUsage * (bunnyVideo!.TotalWatchTime / (double)totalWatchTime))
                    : snapshot?.BandwidthBytes ?? 0;
                var isEstimated = canEstimateBandwidth
                    ? bandwidthBytes > 0
                    : snapshot?.IsBandwidthEstimated ?? false;
                var bandwidthSource = canEstimateBandwidth
                    ? (isEstimated ? "LibraryTrafficAllocatedByWatchTime" : "Unavailable")
                    : snapshot?.BandwidthSource ?? "Unavailable";
                if (isEstimated) estimated++;

                var storageCost = BytesToGb(storageBytes) * settings.BunnyStreamStorageRateUsdPerGb;
                var bandwidthCost = BytesToGb(bandwidthBytes) * settings.BunnyStreamBandwidthRateUsdPerGb;

                if (snapshot is null)
                {
                    snapshot = new BunnyUsageSnapshot
                    {
                        BunnyVideoAssetId = asset.Id,
                        TeacherId = asset.TeacherId,
                        PackageId = asset.PackageId,
                        LessonId = asset.LessonId,
                        PeriodStartUtc = periodStart,
                        PeriodEndUtc = periodEnd,
                        SyncedByUserId = request.CurrentUserId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.BunnyUsageSnapshots.Add(snapshot);
                    created++;
                }
                else
                {
                    if (!request.ForceRefresh)
                    {
                        continue;
                    }
                    updated++;
                }

                snapshot.StorageBytes = storageBytes;
                snapshot.BandwidthBytes = bandwidthBytes;
                snapshot.IsBandwidthEstimated = isEstimated;
                snapshot.BandwidthSource = bandwidthSource;
                snapshot.StorageRateUsdPerGb = settings.BunnyStreamStorageRateUsdPerGb;
                snapshot.BandwidthRateUsdPerGb = settings.BunnyStreamBandwidthRateUsdPerGb;
                snapshot.StorageCostUsd = storageCost;
                snapshot.BandwidthCostUsd = bandwidthCost;
                snapshot.TotalCostUsd = storageCost + bandwidthCost;
                snapshot.BunnyStorageCalculatedAtUtc = storageInfo?.CalculatedAtUtc;
                snapshot.SyncedAtUtc = DateTime.UtcNow;
                snapshot.UpdatedAt = DateTime.UtcNow;

                asset.StorageBytes = storageBytes;
                asset.BandwidthBytes = bandwidthBytes;
                asset.LastUsageSyncedAtUtc = DateTime.UtcNow;
            }

            if (libraryRefreshIncomplete)
            {
                failedLibraries++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<BunnyUsageSyncResultDto>.Ok(new BunnyUsageSyncResultDto(
            periodStart,
            periodEnd,
            created,
            updated,
            estimated,
            settings.BunnyStreamStorageRateUsdPerGb,
            settings.BunnyStreamBandwidthRateUsdPerGb,
            failedLibraries),
            failedLibraries > 0 ? "اكتملت المزامنة جزئيًا؛ تعذر الوصول إلى بعض مكتبات Bunny." : null);
    }

    private static decimal BytesToGb(long bytes) => bytes <= 0 ? 0m : bytes / BytesPerGb;
}
