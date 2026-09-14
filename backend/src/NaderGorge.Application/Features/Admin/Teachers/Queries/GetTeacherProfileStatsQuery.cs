using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Entities;
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
    private readonly ContentGrantFactSource _factSource;

    public GetTeacherProfileStatsQueryHandler(IAppDbContext db)
    {
        _db = db;
        _factSource = new ContentGrantFactSource(db);
    }

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

        var grantFacts = await _factSource.LoadAsync(new ContentGrantFactScope(packageIds), ct);
        var activeStudentsCount = ContentAcquisitionCalculator.CountActiveStudents(grantFacts, DateTime.UtcNow);
        var teacherStudents = ContentAcquisitionCalculator.SummarizeStudents(grantFacts);
        var acquisitionsByPackage = ContentAcquisitionCalculator.SummarizePackages(packageIds, grantFacts);
        var packageSales = teacherPackages.Select(package =>
        {
            var acquisitions = acquisitionsByPackage[package.Id];
            return new TeacherPackageSalesBreakdownDto(
                package.Id,
                package.Name,
                acquisitions.Package.Total,
                acquisitions.Term.Total,
                acquisitions.Section.Total,
                acquisitions.Lesson.Total,
                acquisitions.Overall.Purchased,
                acquisitions.Overall.GiftOnly);
        }).ToArray();

        var dto = new TeacherProfileStatsDto(
            packagesCount,
            teacherStudents.Total,
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
}
