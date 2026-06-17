using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
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
    int EstimatedBandwidthCount,
    IReadOnlyList<BunnyVideoCostDto> Videos,
    IReadOnlyList<BunnyAggregateCostDto> Teachers,
    IReadOnlyList<BunnyAggregateCostDto> Packages);

public record BunnyVideoCostDto(
    Guid LessonVideoId,
    Guid BunnyVideoAssetId,
    string Title,
    Guid TeacherId,
    Guid PackageId,
    long StorageBytes,
    long BandwidthBytes,
    decimal StorageCostUsd,
    decimal BandwidthCostUsd,
    decimal TotalCostUsd,
    bool IsBandwidthEstimated);

public record BunnyAggregateCostDto(Guid Id, string Name, long StorageBytes, long BandwidthBytes, decimal TotalCostUsd);

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

        var snapshots = await snapshotsQuery.AsNoTracking().ToListAsync(cancellationToken);
        var teacherIds = snapshots.Select(snapshot => snapshot.TeacherId).Distinct().ToList();
        var packageIds = snapshots.Select(snapshot => snapshot.PackageId).Distinct().ToList();

        var teacherNames = await _db.TeacherProfiles
            .Include(teacher => teacher.User)
            .Where(teacher => teacherIds.Contains(teacher.Id))
            .ToDictionaryAsync(teacher => teacher.Id, teacher => teacher.User.FullName, cancellationToken);

        var packageNames = await _db.Packages
            .Where(package => packageIds.Contains(package.Id))
            .ToDictionaryAsync(package => package.Id, package => package.Name, cancellationToken);

        var videos = snapshots.Select(snapshot => new BunnyVideoCostDto(
            snapshot.BunnyVideoAsset.LessonVideoId,
            snapshot.BunnyVideoAssetId,
            snapshot.BunnyVideoAsset.Title,
            snapshot.TeacherId,
            snapshot.PackageId,
            snapshot.StorageBytes,
            snapshot.BandwidthBytes,
            snapshot.StorageCostUsd,
            snapshot.BandwidthCostUsd,
            snapshot.TotalCostUsd,
            snapshot.IsBandwidthEstimated)).ToList();

        var teachers = snapshots
            .GroupBy(snapshot => snapshot.TeacherId)
            .Select(group => new BunnyAggregateCostDto(
                group.Key,
                teacherNames.GetValueOrDefault(group.Key, "Unknown teacher"),
                group.Sum(snapshot => snapshot.StorageBytes),
                group.Sum(snapshot => snapshot.BandwidthBytes),
                group.Sum(snapshot => snapshot.TotalCostUsd)))
            .ToList();

        var packages = snapshots
            .GroupBy(snapshot => snapshot.PackageId)
            .Select(group => new BunnyAggregateCostDto(
                group.Key,
                packageNames.GetValueOrDefault(group.Key, "Unknown package"),
                group.Sum(snapshot => snapshot.StorageBytes),
                group.Sum(snapshot => snapshot.BandwidthBytes),
                group.Sum(snapshot => snapshot.TotalCostUsd)))
            .ToList();

        return ApiResponse<BunnyCostReportDto>.Ok(new BunnyCostReportDto(
            periodStart,
            periodEnd,
            snapshots.Sum(snapshot => snapshot.TotalCostUsd),
            snapshots.Sum(snapshot => snapshot.StorageBytes),
            snapshots.Sum(snapshot => snapshot.BandwidthBytes),
            snapshots.Count(snapshot => snapshot.IsBandwidthEstimated),
            videos,
            teachers,
            packages));
    }
}
