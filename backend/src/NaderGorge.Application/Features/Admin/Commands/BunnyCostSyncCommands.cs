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
    decimal BandwidthRateUsdPerGb);

public sealed class SyncBunnyUsageCommandHandler : IRequestHandler<SyncBunnyUsageCommand, ApiResponse<BunnyUsageSyncResultDto>>
{
    private const decimal BytesPerGb = 1024m * 1024m * 1024m;

    private readonly IAppDbContext _db;
    private readonly IBunnyStreamClient _bunny;
    private readonly ICachedPlatformSettingsReader _settingsReader;

    public SyncBunnyUsageCommandHandler(IAppDbContext db, IBunnyStreamClient bunny, ICachedPlatformSettingsReader settingsReader)
    {
        _db = db;
        _bunny = bunny;
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

        var library = await _bunny.GetVideoLibraryAsync(cancellationToken);
        var videos = await _bunny.ListVideosAsync(cancellationToken);
        var totalWatchTime = Math.Max(1, videos.Sum(video => video.TotalWatchTime));
        var trafficUsage = library?.TrafficUsage ?? 0;

        var created = 0;
        var updated = 0;
        var estimated = 0;

        foreach (var asset in assets)
        {
            var bunnyVideo = videos.FirstOrDefault(video => string.Equals(video.Guid, asset.BunnyVideoGuid, StringComparison.OrdinalIgnoreCase))
                ?? await _bunny.GetVideoAsync(asset.BunnyVideoGuid, cancellationToken);
            var storageInfo = await _bunny.GetVideoStorageAsync(asset.BunnyVideoGuid, cancellationToken);

            var storageBytes = storageInfo?.TotalBytes ?? bunnyVideo?.StorageSize ?? asset.StorageBytes ?? 0;
            var bandwidthBytes = bunnyVideo is null ? 0 : (long)Math.Round(trafficUsage * (bunnyVideo.TotalWatchTime / (double)totalWatchTime));
            var isEstimated = bandwidthBytes > 0;
            if (isEstimated)
            {
                estimated++;
            }

            var storageCost = BytesToGb(storageBytes) * settings.BunnyStreamStorageRateUsdPerGb;
            var bandwidthCost = BytesToGb(bandwidthBytes) * settings.BunnyStreamBandwidthRateUsdPerGb;

            var snapshot = await _db.BunnyUsageSnapshots.FirstOrDefaultAsync(s =>
                s.BunnyVideoAssetId == asset.Id &&
                s.PeriodStartUtc == periodStart &&
                s.PeriodEndUtc == periodEnd,
                cancellationToken);

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
            snapshot.BandwidthSource = isEstimated ? "LibraryTrafficAllocatedByWatchTime" : "Unavailable";
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

        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<BunnyUsageSyncResultDto>.Ok(new BunnyUsageSyncResultDto(
            periodStart,
            periodEnd,
            created,
            updated,
            estimated,
            settings.BunnyStreamStorageRateUsdPerGb,
            settings.BunnyStreamBandwidthRateUsdPerGb));
    }

    private static decimal BytesToGb(long bytes) => bytes <= 0 ? 0m : bytes / BytesPerGb;
}
