using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public sealed record GetContentSummaryQuery(
    Guid? TeacherUserId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? TeacherId = null) : IRequest<ApiResponse<ContentSummaryDto>>;

public sealed record ContentAcquisitionCountDto(int Purchased, int Gifts);

public sealed record ContentPackageSummaryDto(
    Guid PackageId,
    string PackageName,
    string TeacherName,
    ContentAcquisitionCountDto Package,
    ContentAcquisitionCountDto Term,
    ContentAcquisitionCountDto Section,
    ContentAcquisitionCountDto Lesson,
    int PurchasedStudents,
    int GiftStudents,
    int TotalStudents);

public sealed record PackageCombinationSummaryDto(
    IReadOnlyList<Guid> PackageIds,
    IReadOnlyList<string> PackageNames,
    int StudentsCount);

public sealed record ContentSummaryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    IReadOnlyList<ContentPackageSummaryDto> Packages,
    IReadOnlyList<PackageCombinationSummaryDto> PackageCombinations);

public sealed class GetContentSummaryQueryHandler
    : IRequestHandler<GetContentSummaryQuery, ApiResponse<ContentSummaryDto>>
{
    private readonly IAppDbContext _db;
    private readonly ContentGrantFactSource _factSource;

    public GetContentSummaryQueryHandler(IAppDbContext db)
    {
        _db = db;
        _factSource = new ContentGrantFactSource(db);
    }

    public async Task<ApiResponse<ContentSummaryDto>> Handle(GetContentSummaryQuery request, CancellationToken ct)
    {
        if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.FromUtc >= request.ToUtc)
            return ApiResponse<ContentSummaryDto>.Fail("تاريخ البداية يجب أن يسبق تاريخ النهاية");

        if (request.TeacherUserId.HasValue && request.TeacherId.HasValue)
            return ApiResponse<ContentSummaryDto>.Fail("لا يمكن تحديد حساب المعلم ومعرف المعلم معاً");

        var teacherId = request.TeacherId;
        if (request.TeacherUserId.HasValue)
        {
            teacherId = await _db.TeacherProfiles.AsNoTracking()
                .Where(teacher => teacher.UserId == request.TeacherUserId.Value)
                .Select(teacher => (Guid?)teacher.Id)
                .SingleOrDefaultAsync(ct);

            if (!teacherId.HasValue)
                return ApiResponse<ContentSummaryDto>.Fail("حساب المعلم غير موجود");
        }
        else if (teacherId.HasValue && !await _db.TeacherProfiles.AsNoTracking()
                     .AnyAsync(teacher => teacher.Id == teacherId.Value, ct))
        {
            return ApiResponse<ContentSummaryDto>.Fail("حساب المعلم غير موجود");
        }

        var packagesQuery = _db.Packages.AsNoTracking().AsQueryable();
        if (teacherId.HasValue)
            packagesQuery = packagesQuery.Where(package => package.TeacherId == teacherId.Value);

        var packages = await packagesQuery
            .OrderBy(package => package.Name)
            .Select(package => new PackageRow(
                package.Id,
                package.Name,
                package.Teacher != null ? package.Teacher.User.FullName : string.Empty))
            .ToListAsync(ct);

        var packageIds = packages.Select(package => package.Id).ToArray();
        var grants = await _factSource.LoadAsync(
            new ContentGrantFactScope(packageIds, request.FromUtc, request.ToUtc),
            ct);
        var acquisitionsByPackage = ContentAcquisitionCalculator.SummarizePackages(packageIds, grants);

        var summaries = packages.Select(package =>
        {
            var acquisitions = acquisitionsByPackage[package.Id];

            return new ContentPackageSummaryDto(
                package.Id,
                package.Name,
                package.TeacherName,
                ToDto(acquisitions.Package),
                ToDto(acquisitions.Term),
                ToDto(acquisitions.Section),
                ToDto(acquisitions.Lesson),
                acquisitions.Overall.Purchased,
                acquisitions.Overall.GiftOnly,
                acquisitions.Overall.Total);
        }).ToArray();

        var packageNames = packages.ToDictionary(package => package.Id, package => package.Name);
        var combinations = grants
            .Where(grant => !grant.IsGift)
            .GroupBy(grant => grant.UserId)
            .Select(group => group.Select(grant => grant.PackageId).Distinct().Order().ToArray())
            .Where(ids => ids.Length > 1)
            .GroupBy(ids => string.Join('|', ids))
            .Select(group => new PackageCombinationSummaryDto(
                group.First(),
                group.First().Select(id => packageNames[id]).ToArray(),
                group.Count()))
            .OrderByDescending(combination => combination.StudentsCount)
            .ThenBy(combination => string.Join('|', combination.PackageNames))
            .ToArray();

        return ApiResponse<ContentSummaryDto>.Ok(new ContentSummaryDto(
            request.FromUtc,
            request.ToUtc,
            summaries,
            combinations));
    }

    private static ContentAcquisitionCountDto ToDto(ContentAcquisitionStudentCounts counts) =>
        new(counts.Purchased, counts.GiftOnly);

    private sealed record PackageRow(Guid Id, string Name, string TeacherName);
}
