using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public sealed record GetContentSummaryQuery(
    Guid? TeacherUserId,
    DateTime? FromUtc,
    DateTime? ToUtc) : IRequest<ApiResponse<ContentSummaryDto>>;

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

    public GetContentSummaryQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<ContentSummaryDto>> Handle(GetContentSummaryQuery request, CancellationToken ct)
    {
        if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.FromUtc > request.ToUtc)
            return ApiResponse<ContentSummaryDto>.Fail("تاريخ البداية يجب أن يسبق تاريخ النهاية");

        Guid? teacherId = null;
        if (request.TeacherUserId.HasValue)
        {
            teacherId = await _db.TeacherProfiles.AsNoTracking()
                .Where(teacher => teacher.UserId == request.TeacherUserId.Value)
                .Select(teacher => (Guid?)teacher.Id)
                .SingleOrDefaultAsync(ct);

            if (!teacherId.HasValue)
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
        var grants = await LoadGrantRowsAsync(packageIds, request.FromUtc, request.ToUtc, ct);

        var summaries = packages.Select(package =>
        {
            var packageGrants = grants.Where(grant => grant.PackageId == package.Id).ToArray();
            var purchasedStudentIds = packageGrants.Where(grant => !grant.IsGift).Select(grant => grant.UserId).ToHashSet();
            var giftStudents = packageGrants
                .Where(grant => grant.IsGift && !purchasedStudentIds.Contains(grant.UserId))
                .Select(grant => grant.UserId)
                .Distinct()
                .Count();

            return new ContentPackageSummaryDto(
                package.Id,
                package.Name,
                package.TeacherName,
                Count(packageGrants, CodeType.Package),
                Count(packageGrants, CodeType.Term),
                Count(packageGrants, CodeType.Month),
                Count(packageGrants, CodeType.Lesson),
                purchasedStudentIds.Count,
                giftStudents,
                packageGrants.Select(grant => grant.UserId).Distinct().Count());
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

    private async Task<List<GrantRow>> LoadGrantRowsAsync(
        Guid[] packageIds,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct)
    {
        if (packageIds.Length == 0) return [];

        var rows = await Filter(PackageGrants(packageIds), fromUtc, toUtc).ToListAsync(ct);
        rows.AddRange(await Filter(TermGrants(packageIds), fromUtc, toUtc).ToListAsync(ct));
        rows.AddRange(await Filter(SectionGrants(packageIds), fromUtc, toUtc).ToListAsync(ct));
        rows.AddRange(await Filter(LessonGrants(packageIds), fromUtc, toUtc).ToListAsync(ct));
        return rows;
    }

    private IQueryable<GrantRow> PackageGrants(Guid[] packageIds) =>
        _db.StudentAccessGrants.AsNoTracking()
            .Where(grant => !grant.CancelledAt.HasValue && grant.PackageId.HasValue && packageIds.Contains(grant.PackageId.Value))
            .Select(grant => new GrantRow(grant.PackageId!.Value, grant.PackageId.Value, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue, grant.GrantedAt));

    private IQueryable<GrantRow> TermGrants(Guid[] packageIds) =>
        from grant in _db.StudentAccessGrants.AsNoTracking()
        join term in _db.Terms.AsNoTracking() on grant.TermId equals term.Id
        where !grant.CancelledAt.HasValue && packageIds.Contains(term.PackageId)
        select new GrantRow(term.PackageId, term.Id, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue, grant.GrantedAt);

    private IQueryable<GrantRow> SectionGrants(Guid[] packageIds) =>
        from grant in _db.StudentAccessGrants.AsNoTracking()
        join section in _db.ContentSections.AsNoTracking() on grant.ContentSectionId equals section.Id
        join term in _db.Terms.AsNoTracking() on section.TermId equals term.Id
        where !grant.CancelledAt.HasValue && packageIds.Contains(term.PackageId)
        select new GrantRow(term.PackageId, section.Id, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue, grant.GrantedAt);

    private IQueryable<GrantRow> LessonGrants(Guid[] packageIds) =>
        from grant in _db.StudentAccessGrants.AsNoTracking()
        join lesson in _db.Lessons.AsNoTracking() on grant.LessonId equals lesson.Id
        join section in _db.ContentSections.AsNoTracking() on lesson.ContentSectionId equals section.Id
        join term in _db.Terms.AsNoTracking() on section.TermId equals term.Id
        where !grant.CancelledAt.HasValue && packageIds.Contains(term.PackageId)
        select new GrantRow(term.PackageId, lesson.Id, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue, grant.GrantedAt);

    private static IQueryable<GrantRow> Filter(IQueryable<GrantRow> query, DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue) query = query.Where(grant => grant.GrantedAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(grant => grant.GrantedAt < toUtc.Value);
        return query;
    }

    private static ContentAcquisitionCountDto Count(IEnumerable<GrantRow> grants, CodeType type)
    {
        var acquisitions = grants
            .Where(grant => grant.GrantType == type)
            .GroupBy(grant => new { grant.UserId, grant.TargetId })
            .Select(group => new { group.Key.UserId, IsGiftOnly = group.All(grant => grant.IsGift) })
            .ToArray();

        return new ContentAcquisitionCountDto(
            acquisitions.Where(acquisition => !acquisition.IsGiftOnly).Select(acquisition => acquisition.UserId).Distinct().Count(),
            acquisitions.Where(acquisition => acquisition.IsGiftOnly).Select(acquisition => acquisition.UserId).Distinct().Count());
    }

    private sealed record PackageRow(Guid Id, string Name, string TeacherName);
    private sealed record GrantRow(Guid PackageId, Guid TargetId, Guid UserId, CodeType GrantType, bool IsGift, DateTime GrantedAt);
}
