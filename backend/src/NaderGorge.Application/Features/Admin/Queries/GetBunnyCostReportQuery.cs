using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetBunnyCostReportQuery(DateTime PeriodStart, DateTime PeriodEnd, Guid? TeacherId, Guid? PackageId)
    : IRequest<ApiResponse<BunnyCostReportDto>>;

public record BunnyCostReportDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal PlatformTotalCostUsd,
    long PlatformStorageBytes,
    long PlatformBandwidthBytes,
    int ActualBandwidthCount,
    int EstimatedBandwidthCount,
    int MissingBandwidthCount,
    int DuplicateSnapshotCount,
    DateTime? LastSyncedAtUtc,
    IReadOnlyList<BunnyVideoCostDto> Videos,
    IReadOnlyList<BunnyAggregateCostDto> Teachers,
    IReadOnlyList<BunnyAggregateCostDto> Packages);

public record BunnyVideoCostDto(
    Guid LessonVideoId,
    Guid BunnyVideoAssetId,
    string Title,
    Guid TeacherId,
    Guid PackageId,
    Guid LessonId,
    long StorageBytes,
    long BandwidthBytes,
    decimal StorageCostUsd,
    decimal BandwidthCostUsd,
    decimal TotalCostUsd,
    bool IsBandwidthEstimated,
    string BandwidthSource,
    string BandwidthDataQuality,
    DateTime? LastSyncedAtUtc);

public record BunnyAggregateCostDto(
    Guid Id,
    string Name,
    long StorageBytes,
    long BandwidthBytes,
    decimal TotalCostUsd,
    int ActualBandwidthCount,
    int EstimatedBandwidthCount,
    int MissingBandwidthCount,
    DateTime? LastSyncedAtUtc);

public sealed class GetBunnyCostReportQueryHandler : IRequestHandler<GetBunnyCostReportQuery, ApiResponse<BunnyCostReportDto>>
{
    private readonly IAppDbContext _db;

    public GetBunnyCostReportQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<BunnyCostReportDto>> Handle(GetBunnyCostReportQuery request, CancellationToken cancellationToken)
    {
        var periodStart = DateTime.SpecifyKind(request.PeriodStart.Date, DateTimeKind.Utc);
        var periodEnd = DateTime.SpecifyKind(request.PeriodEnd.Date, DateTimeKind.Utc);

        var snapshotsQuery = _db.BunnyUsageSnapshots
            .Include(snapshot => snapshot.BunnyVideoAsset).ThenInclude(asset => asset.LessonVideo)
            .Where(snapshot => snapshot.PeriodStartUtc >= periodStart && snapshot.PeriodEndUtc <= periodEnd);

        if (request.TeacherId.HasValue)
        {
            snapshotsQuery = snapshotsQuery.Where(snapshot => snapshot.TeacherId == request.TeacherId.Value);
        }
        if (request.PackageId.HasValue)
        {
            snapshotsQuery = snapshotsQuery.Where(snapshot => snapshot.PackageId == request.PackageId.Value);
        }

        var rawSnapshots = await snapshotsQuery.AsNoTracking().ToListAsync(cancellationToken);

        // The database has a unique index for this key. Grouping defensively here also makes the
        // report correct for historic/imported data that predates the index, without ever charging
        // a video twice for the same reporting interval.
        var snapshots = rawSnapshots
            .GroupBy(snapshot => new { snapshot.BunnyVideoAssetId, snapshot.PeriodStartUtc, snapshot.PeriodEndUtc })
            .Select(group => group
                .OrderByDescending(snapshot => snapshot.SyncedAtUtc)
                .ThenByDescending(snapshot => snapshot.Id)
                .First())
            .ToList();
        var duplicateSnapshotCount = rawSnapshots.Count - snapshots.Count;
        var teacherIds = snapshots.Select(snapshot => snapshot.TeacherId).Distinct().ToList();
        var packageIds = snapshots.Select(snapshot => snapshot.PackageId).Distinct().ToList();

        var teacherNames = await _db.TeacherProfiles
            .Include(teacher => teacher.User)
            .Where(teacher => teacherIds.Contains(teacher.Id))
            .ToDictionaryAsync(teacher => teacher.Id, teacher => teacher.User.FullName, cancellationToken);

        var packageNames = await _db.Packages
            .Where(package => packageIds.Contains(package.Id))
            .ToDictionaryAsync(package => package.Id, package => package.Name, cancellationToken);

        // A video can have more than one snapshot in a requested month (for example, a backfill
        // followed by a regular sync). Return it once and roll its period costs into that row.
        var videos = snapshots
            .GroupBy(snapshot => snapshot.BunnyVideoAssetId)
            .Select(group => ToVideoCost(group.ToList()))
            .OrderBy(video => video.Title)
            .ToList();

        var teachers = videos
            .GroupBy(video => video.TeacherId)
            .Select(group => ToAggregate(group.Key, teacherNames.GetValueOrDefault(group.Key, "Unknown teacher"), group))
            .OrderBy(teacher => teacher.Name)
            .ToList();

        var packages = videos
            .GroupBy(video => video.PackageId)
            .Select(group => ToAggregate(group.Key, packageNames.GetValueOrDefault(group.Key, "Unknown package"), group))
            .OrderBy(package => package.Name)
            .ToList();

        return ApiResponse<BunnyCostReportDto>.Ok(new BunnyCostReportDto(
            periodStart,
            periodEnd,
            snapshots.Sum(snapshot => snapshot.TotalCostUsd),
            snapshots.Sum(snapshot => snapshot.StorageBytes),
            snapshots.Sum(snapshot => snapshot.BandwidthBytes),
            videos.Count(video => video.BandwidthDataQuality == "Actual"),
            videos.Count(video => video.BandwidthDataQuality == "Estimated"),
            videos.Count(video => video.BandwidthDataQuality == "Missing"),
            duplicateSnapshotCount,
            snapshots.Count == 0 ? null : snapshots.Max(snapshot => snapshot.SyncedAtUtc),
            videos,
            teachers,
            packages));
    }

    private static BunnyVideoCostDto ToVideoCost(IReadOnlyList<BunnyUsageSnapshot> snapshots)
    {
        var latest = snapshots.OrderByDescending(snapshot => snapshot.SyncedAtUtc).ThenByDescending(snapshot => snapshot.Id).First();
        var dataQuality = GetDataQuality(snapshots);
        var sources = snapshots.Select(snapshot => NormalizeSource(snapshot.BandwidthSource)).Distinct().OrderBy(source => source);

        return new BunnyVideoCostDto(
            latest.BunnyVideoAsset.LessonVideoId,
            latest.BunnyVideoAssetId,
            latest.BunnyVideoAsset.Title,
            latest.TeacherId,
            latest.PackageId,
            latest.LessonId,
            snapshots.Sum(snapshot => snapshot.StorageBytes),
            snapshots.Sum(snapshot => snapshot.BandwidthBytes),
            snapshots.Sum(snapshot => snapshot.StorageCostUsd),
            snapshots.Sum(snapshot => snapshot.BandwidthCostUsd),
            snapshots.Sum(snapshot => snapshot.TotalCostUsd),
            dataQuality == "Estimated",
            string.Join("; ", sources),
            dataQuality,
            snapshots.Max(snapshot => snapshot.SyncedAtUtc));
    }

    private static BunnyAggregateCostDto ToAggregate(Guid id, string name, IEnumerable<BunnyVideoCostDto> videos)
    {
        var list = videos.ToList();
        return new BunnyAggregateCostDto(
            id,
            name,
            list.Sum(video => video.StorageBytes),
            list.Sum(video => video.BandwidthBytes),
            list.Sum(video => video.TotalCostUsd),
            list.Count(video => video.BandwidthDataQuality == "Actual"),
            list.Count(video => video.BandwidthDataQuality == "Estimated"),
            list.Count(video => video.BandwidthDataQuality == "Missing"),
            list.Count == 0 ? null : list.Max(video => video.LastSyncedAtUtc));
    }

    private static string GetDataQuality(IEnumerable<BunnyUsageSnapshot> snapshots)
    {
        var list = snapshots.ToList();
        if (list.Any(snapshot => IsMissing(snapshot))) return "Missing";
        return list.Any(snapshot => snapshot.IsBandwidthEstimated) ? "Estimated" : "Actual";
    }

    private static bool IsMissing(BunnyUsageSnapshot snapshot) =>
        snapshot.BandwidthBytes <= 0 || string.Equals(NormalizeSource(snapshot.BandwidthSource), "Unavailable", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSource(string? source) => string.IsNullOrWhiteSpace(source) ? "Unavailable" : source.Trim();
}
