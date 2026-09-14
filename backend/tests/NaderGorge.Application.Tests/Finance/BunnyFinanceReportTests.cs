using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class BunnyFinanceReportTests
{
    [Fact]
    public async Task Handle_rolls_each_video_once_and_keeps_actual_estimated_and_missing_provenance()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Bunny Teacher", "01020000001");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id };
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Bunny package");
        db.TeacherProfiles.Add(teacher);

        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var actualAsset = CreateAsset(teacher.Id, packageId, teacherUser.Id, "Actual video");
        var estimatedAsset = CreateAsset(teacher.Id, packageId, teacherUser.Id, "Estimated video");
        var missingAsset = CreateAsset(teacher.Id, packageId, teacherUser.Id, "Missing video");
        db.BunnyVideoAssets.AddRange(actualAsset, estimatedAsset, missingAsset);
        db.BunnyUsageSnapshots.AddRange(
            // Historic/imported duplicate: the newest snapshot must win for that exact interval.
            CreateSnapshot(actualAsset, periodStart, periodEnd, 1m, 100, false, "BunnyVideoAnalytics", periodEnd.AddDays(2)),
            CreateSnapshot(actualAsset, periodStart, periodEnd, 10m, 999, false, "BunnyVideoAnalytics", periodEnd.AddDays(1)),
            CreateSnapshot(estimatedAsset, periodStart, periodEnd, 2m, 200, true, "LibraryTrafficAllocatedByWatchTime", periodEnd.AddDays(2)),
            CreateSnapshot(missingAsset, periodStart, periodEnd, 3m, 0, false, "Unavailable", periodEnd.AddDays(2)));
        // EGP finance data is deliberately unrelated to this USD-only report.
        db.TeacherFinancialEvents.Add(new TeacherFinancialEvent
        {
            Id = Guid.NewGuid(), SourceId = Guid.NewGuid(), SourceType = TeacherFinancialSourceType.DirectPurchase,
            TargetId = packageId, TargetType = SalesTargetType.Package, GrossAmount = 9999m, OccurredAt = periodStart
        });
        await db.SaveChangesAsync();

        var result = await new GetBunnyCostReportQueryHandler(db)
            .Handle(new GetBunnyCostReportQuery(periodStart, periodEnd, null, null), CancellationToken.None);

        Assert.True(result.Success);
        var report = Assert.IsType<BunnyCostReportDto>(result.Data);
        Assert.Equal(3, report.Videos.Count);
        Assert.Equal(1, report.DuplicateSnapshotCount);
        Assert.Equal(6m, report.PlatformTotalCostUsd);
        Assert.Equal(1, report.ActualBandwidthCount);
        Assert.Equal(1, report.EstimatedBandwidthCount);
        Assert.Equal(1, report.MissingBandwidthCount);
        Assert.Single(report.Teachers);
        Assert.Equal(6m, report.Teachers[0].TotalCostUsd);
        Assert.Single(report.Packages);
        Assert.Equal(6m, report.Packages[0].TotalCostUsd);
        Assert.Equal("Actual", report.Videos.Single(video => video.Title == "Actual video").BandwidthDataQuality);
        Assert.Equal("Estimated", report.Videos.Single(video => video.Title == "Estimated video").BandwidthDataQuality);
        Assert.Equal("Missing", report.Videos.Single(video => video.Title == "Missing video").BandwidthDataQuality);
    }

    [Fact]
    public async Task Handle_does_not_treat_a_zero_or_unavailable_bandwidth_value_as_actual()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Missing Bunny Teacher", "01020000002");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id };
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Missing Bunny package");
        var asset = CreateAsset(teacher.Id, packageId, teacherUser.Id, "No traffic");
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TeacherProfiles.Add(teacher);
        db.BunnyVideoAssets.Add(asset);
        db.BunnyUsageSnapshots.Add(CreateSnapshot(asset, periodStart, periodStart.AddMonths(1), 0m, 0, false, "Unavailable", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var result = await new GetBunnyCostReportQueryHandler(db)
            .Handle(new GetBunnyCostReportQuery(periodStart, periodStart.AddMonths(1), null, null), CancellationToken.None);

        var report = Assert.IsType<BunnyCostReportDto>(result.Data);
        Assert.Equal(0, report.ActualBandwidthCount);
        Assert.Equal(1, report.MissingBandwidthCount);
        Assert.Equal("Missing", Assert.Single(report.Videos).BandwidthDataQuality);
    }

    private static BunnyVideoAsset CreateAsset(Guid teacherId, Guid packageId, Guid uploaderId, string title)
    {
        var lessonId = Guid.NewGuid();
        var video = new LessonVideo { Id = Guid.NewGuid(), LessonId = lessonId, Title = title, Provider = "bunny", ProviderVideoId = Guid.NewGuid().ToString("N") };
        return new BunnyVideoAsset
        {
            Id = Guid.NewGuid(), LessonVideoId = video.Id, LessonVideo = video, LessonId = lessonId,
            TeacherId = teacherId, PackageId = packageId, UploadedByUserId = uploaderId,
            BunnyLibraryId = 1, BunnyVideoGuid = Guid.NewGuid().ToString("N"), Title = title, UploadMethod = "Test"
        };
    }

    private static BunnyUsageSnapshot CreateSnapshot(BunnyVideoAsset asset, DateTime start, DateTime end, decimal cost, long bandwidth,
        bool estimated, string source, DateTime syncedAt) => new()
    {
        Id = Guid.NewGuid(), BunnyVideoAssetId = asset.Id, BunnyVideoAsset = asset, TeacherId = asset.TeacherId,
        PackageId = asset.PackageId, LessonId = asset.LessonId, PeriodStartUtc = start, PeriodEndUtc = end,
        StorageBytes = 1024, BandwidthBytes = bandwidth, StorageCostUsd = cost, BandwidthCostUsd = 0m,
        TotalCostUsd = cost, IsBandwidthEstimated = estimated, BandwidthSource = source, SyncedAtUtc = syncedAt
    };
}
