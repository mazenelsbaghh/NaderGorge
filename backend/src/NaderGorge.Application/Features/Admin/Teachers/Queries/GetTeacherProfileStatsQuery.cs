using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Teachers.Queries;

public record GetTeacherProfileStatsQuery(Guid TeacherId) : IRequest<ApiResponse<TeacherProfileStatsDto>>;

public record TeacherProfileStatsDto(
    int PackagesCount,
    int StudentsCount,
    int ActiveStudentsCount,
    decimal TotalEarnings,
    decimal CurrentBalance,
    int ExamsCount,
    int EssaysPendingCount,
    int EssaysGradedCount,
    int CodeGroupsCount,
    int QuestionBankItemsCount,
    IReadOnlyList<TeacherPackageSalesBreakdownDto> PackageSales
);

public record TeacherPackageSalesBreakdownDto(
    Guid PackageId,
    string PackageName,
    int PackageBuyers,
    int TermBuyers,
    int SectionBuyers,
    int LessonBuyers,
    int PurchasedStudents,
    int GiftStudents
);

public class GetTeacherProfileStatsQueryHandler : IRequestHandler<GetTeacherProfileStatsQuery, ApiResponse<TeacherProfileStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetTeacherProfileStatsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<TeacherProfileStatsDto>> Handle(GetTeacherProfileStatsQuery request, CancellationToken ct)
    {
        var teacherExists = await _db.TeacherProfiles.AnyAsync(tp => tp.Id == request.TeacherId, ct);
        if (!teacherExists)
            return ApiResponse<TeacherProfileStatsDto>.Fail("Teacher profile not found");

        var teacherPackages = await _db.Packages.AsNoTracking()
            .Where(package => package.TeacherId == request.TeacherId)
            .OrderBy(package => package.Name)
            .Select(package => new { package.Id, package.Name })
            .ToListAsync(ct);
        var packageIds = teacherPackages.Select(package => package.Id).ToArray();
        var packagesCount = teacherPackages.Count;

        // Count distinct students enrolled across all teacher's packages (Package-level only)
        var activeStudentsCount = await _db.StudentAccessGrants
            .Where(sag => sag.GrantType == Domain.Enums.CodeType.Package && sag.PackageId != null && sag.IsActive)
            .Where(sag => _db.Packages
                .Where(p => p.TeacherId == request.TeacherId)
                .Select(p => p.Id)
                .Contains(sag.PackageId!.Value))
            .Select(sag => sag.UserId)
            .Distinct()
            .CountAsync(ct);

        // Get TeacherAccount earnings/balance
        var account = await _db.TeacherAccounts
            .FirstOrDefaultAsync(ta => ta.TeacherId == request.TeacherId, ct);

        var totalEarnings = account?.TotalEarnings ?? 0m;
        var currentBalance = account?.CurrentBalance ?? 0m;

        var examsCount = await _db.Exams
            .CountAsync(e => e.CreatedByTeacherId == request.TeacherId, ct);

        var essaysPendingCount = await _db.EssaySubmissions
            .CountAsync(e => e.GradedByTeacherId == request.TeacherId
                && e.Status != EssaySubmissionStatus.TeacherGraded, ct);

        var essaysGradedCount = await _db.EssaySubmissions
            .CountAsync(e => e.GradedByTeacherId == request.TeacherId
                && e.Status == EssaySubmissionStatus.TeacherGraded, ct);

        var codeGroupsCount = await _db.CodeGroups
            .CountAsync(cg => cg.TeacherId == request.TeacherId, ct);

        var questionBankItemsCount = await _db.QuestionBankItems
            .CountAsync(q => q.CreatedByTeacherId == request.TeacherId, ct);

        var grantRows = await PackageGrantRows(packageIds)
            .Concat(TermGrantRows(packageIds))
            .Concat(SectionGrantRows(packageIds))
            .Concat(LessonGrantRows(packageIds))
            .ToListAsync(ct);
        var packageSales = teacherPackages.Select(package =>
        {
            var rows = grantRows.Where(row => row.PackageId == package.Id).ToArray();
            return new TeacherPackageSalesBreakdownDto(
                package.Id,
                package.Name,
                DistinctStudents(rows, CodeType.Package),
                DistinctStudents(rows, CodeType.Term),
                DistinctStudents(rows, CodeType.Month),
                DistinctStudents(rows, CodeType.Lesson),
                rows.Where(row => !row.IsGift).Select(row => row.UserId).Distinct().Count(),
                rows.Where(row => row.IsGift).Select(row => row.UserId).Distinct().Count());
        }).ToArray();
        var studentsCount = grantRows.Select(row => row.UserId).Distinct().Count();

        var dto = new TeacherProfileStatsDto(
            packagesCount,
            studentsCount,
            activeStudentsCount,
            totalEarnings,
            currentBalance,
            examsCount,
            essaysPendingCount,
            essaysGradedCount,
            codeGroupsCount,
            questionBankItemsCount,
            packageSales
        );

        return ApiResponse<TeacherProfileStatsDto>.Ok(dto);
    }

    private IQueryable<TeacherPackageGrantRow> PackageGrantRows(Guid[] packageIds) =>
        _db.StudentAccessGrants.AsNoTracking()
            .Where(grant => !grant.CancelledAt.HasValue && grant.PackageId.HasValue && packageIds.Contains(grant.PackageId.Value))
            .Select(grant => new TeacherPackageGrantRow(grant.PackageId!.Value, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue));

    private IQueryable<TeacherPackageGrantRow> TermGrantRows(Guid[] packageIds) =>
        from grant in _db.StudentAccessGrants.AsNoTracking()
        join term in _db.Terms.AsNoTracking() on grant.TermId equals term.Id
        where !grant.CancelledAt.HasValue && packageIds.Contains(term.PackageId)
        select new TeacherPackageGrantRow(term.PackageId, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue);

    private IQueryable<TeacherPackageGrantRow> SectionGrantRows(Guid[] packageIds) =>
        from grant in _db.StudentAccessGrants.AsNoTracking()
        join section in _db.ContentSections.AsNoTracking() on grant.ContentSectionId equals section.Id
        where !grant.CancelledAt.HasValue && packageIds.Contains(section.Term.PackageId)
        select new TeacherPackageGrantRow(section.Term.PackageId, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue);

    private IQueryable<TeacherPackageGrantRow> LessonGrantRows(Guid[] packageIds) =>
        from grant in _db.StudentAccessGrants.AsNoTracking()
        join lesson in _db.Lessons.AsNoTracking() on grant.LessonId equals lesson.Id
        where !grant.CancelledAt.HasValue && packageIds.Contains(lesson.ContentSection.Term.PackageId)
        select new TeacherPackageGrantRow(lesson.ContentSection.Term.PackageId, grant.UserId, grant.GrantType, grant.GiftRecipientId.HasValue);

    private static int DistinctStudents(IEnumerable<TeacherPackageGrantRow> rows, CodeType grantType) =>
        rows.Where(row => row.GrantType == grantType).Select(row => row.UserId).Distinct().Count();

    private sealed record TeacherPackageGrantRow(Guid PackageId, Guid UserId, CodeType GrantType, bool IsGift);
}
