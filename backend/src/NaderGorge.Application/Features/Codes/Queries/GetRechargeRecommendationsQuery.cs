using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Codes.Queries;

public sealed record GetRechargeRecommendationsQuery(Guid StudentId, Guid RedemptionId, int Page = 1)
    : IRequest<ApiResponse<RechargeRecommendationsDto>>;

public sealed record RechargeRecommendationDto(CodeType ContentType, Guid ContentId, string Title, decimal Price,
    string PackageName, string? TermName, string? SectionName, string OpenUrl);
public sealed record RechargeRecommendationsDto(Guid TeacherId, string TeacherName, decimal RechargedAmount,
    decimal AvailableBalance, int Page, bool HasMore, IReadOnlyList<RechargeRecommendationDto> Items);

public sealed class GetRechargeRecommendationsQueryHandler(IAppDbContext db, IAcademicScopeService academicScope)
    : IRequestHandler<GetRechargeRecommendationsQuery, ApiResponse<RechargeRecommendationsDto>>
{
    private const int PageSize = 24;

    public async Task<ApiResponse<RechargeRecommendationsDto>> Handle(GetRechargeRecommendationsQuery request, CancellationToken ct)
    {
        if (request.Page is < 1 or > 100)
            return ApiResponse<RechargeRecommendationsDto>.Fail("رقم الصفحة غير صالح.");
        var recharge = await db.PromotionalBalanceAllocations.AsNoTracking()
            .Where(allocation => allocation.Id == request.RedemptionId && allocation.StudentId == request.StudentId && allocation.TeacherId != null)
            .Select(allocation => new { TeacherId = allocation.TeacherId!.Value, allocation.OriginalAmount,
                TeacherName = allocation.Teacher!.User.FullName }).FirstOrDefaultAsync(ct);
        if (recharge is null) return ApiResponse<RechargeRecommendationsDto>.Fail("عملية الشحن غير موجودة.");

        var available = await AvailableBalanceAsync(request.StudentId, recharge.TeacherId, ct);
        var candidates = await CandidatesAsync(recharge.TeacherId, available, ct);
        var eligiblePackages = await academicScope.GetEligiblePackageIdsForStudentAsync(
            candidates.Select(candidate => candidate.PackageId).Distinct().ToArray(), request.StudentId, ct);
        var eligibleLessons = await academicScope.GetEligibleLessonIdsForStudentAsync(
            candidates.Where(candidate => candidate.ContentType == CodeType.Lesson).Select(candidate => candidate.ContentId).ToArray(), request.StudentId, ct);
        var now = DateTime.UtcNow;
        var grants = await db.StudentAccessGrants.AsNoTracking().Where(grant => grant.UserId == request.StudentId
            && grant.IsActive && (grant.ExpiresAt == null || grant.ExpiresAt > now)).ToListAsync(ct);
        var ordered = candidates.Where(candidate => eligiblePackages.Contains(candidate.PackageId) && !Owned(candidate, grants))
            .OrderBy(candidate => candidate.Price == recharge.OriginalAmount ? 0 : candidate.Price < recharge.OriginalAmount ? 1 : 2)
            .ThenByDescending(candidate => candidate.CreatedAt).ThenBy(candidate => candidate.ContentId).ThenBy(candidate => candidate.ContentType);
        var pageItems = new List<RechargeRecommendationDto>();
        var skip = (request.Page - 1) * PageSize;
        foreach (var candidate in ordered)
        {
            var eligible = candidate.ContentType == CodeType.Lesson
                ? eligibleLessons.Contains(candidate.ContentId)
                : await academicScope.IsOwnerEligibleForStudentAsync(OwnerType(candidate.ContentType), candidate.ContentId, request.StudentId, ct);
            if (!eligible) continue;
            if (skip > 0) { skip--; continue; }
            pageItems.Add(candidate.ToDto());
            if (pageItems.Count > PageSize) break;
        }
        return ApiResponse<RechargeRecommendationsDto>.Ok(new(recharge.TeacherId, recharge.TeacherName,
            recharge.OriginalAmount, available, request.Page, pageItems.Count > PageSize, pageItems.Take(PageSize).ToArray()));
    }

    private async Task<decimal> AvailableBalanceAsync(Guid studentId, Guid teacherId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var paid = await db.StudentBalances.Where(balance => balance.UserId == studentId)
            .Select(balance => balance.CurrentBalance).FirstOrDefaultAsync(ct);
        var scoped = await db.PromotionalBalanceAllocations.Where(allocation => allocation.StudentId == studentId
            && (allocation.TeacherId == null || allocation.TeacherId == teacherId)
            && (allocation.Status == PromotionalBalanceStatus.Active || allocation.Status == PromotionalBalanceStatus.PartiallyUsed)
            && (allocation.ExpiresAt == null || allocation.ExpiresAt > now)
            && (allocation.MaxPurchaseCount == null || allocation.PurchaseCount < allocation.MaxPurchaseCount))
            .SumAsync(allocation => allocation.AvailableAmount, ct);
        return paid + scoped;
    }

    private async Task<List<Candidate>> CandidatesAsync(Guid teacherId, decimal available, CancellationToken ct)
    {
        var packages = db.Packages.AsNoTracking().Where(package => package.TeacherId == teacherId && package.IsActive
            && package.Teacher.IsContentVisibleToStudents && package.ArchiveMode == ContentArchiveMode.None);
        var packageIds = packages.Select(package => package.Id);
        var terms = db.Terms.AsNoTracking().Where(term => packageIds.Contains(term.PackageId) && term.ArchiveMode == ContentArchiveMode.None);
        var termIds = terms.Select(term => term.Id);
        var sections = db.ContentSections.AsNoTracking().Where(section => termIds.Contains(section.TermId) && section.ArchiveMode == ContentArchiveMode.None);
        var sectionIds = sections.Select(section => section.Id);
        var candidates = await packages.Where(package => package.Price > 0 && package.Price <= available
                && (package.ContentMode != PackageContentMode.TermWithSections || package.AllowFullPackagePurchase)
                && package.ContentMode != PackageContentMode.SectionWithLessons && package.ContentMode != PackageContentMode.LessonsOnly
                && package.ContentMode != PackageContentMode.SingleLesson)
            .Select(package => new Candidate(CodeType.Package, package.Id, package.Name, package.Price, package.CreatedAt,
                package.Id, null, null, package.Name, null, null)).ToListAsync(ct);
        candidates.AddRange(await terms.Where(term => !term.IsSystemContainer || term.Package.ContentMode == PackageContentMode.SectionWithLessons)
            .Select(term => new Candidate(CodeType.Term, term.Id, term.IsSystemContainer ? term.Package.Name : term.Title,
                term.IsSystemContainer ? term.Package.Price : term.Price, term.IsSystemContainer ? term.Package.CreatedAt : term.CreatedAt,
                term.PackageId, term.Id, null, term.Package.Name, term.IsSystemContainer ? null : term.Title, null)).ToListAsync(ct));
        candidates.AddRange(await sections.Where(section => !section.IsSystemContainer || section.Term.Package.ContentMode == PackageContentMode.LessonsOnly)
            .Select(section => new Candidate(CodeType.Month, section.Id, section.IsSystemContainer ? section.Term.Package.Name : section.Title,
                section.IsSystemContainer ? section.Term.Package.Price : section.Price, section.IsSystemContainer ? section.Term.Package.CreatedAt : section.CreatedAt,
                section.Term.PackageId, section.TermId, section.Id, section.Term.Package.Name,
                section.Term.IsSystemContainer ? null : section.Term.Title, section.IsSystemContainer ? null : section.Title)).ToListAsync(ct));
        candidates.AddRange(await db.Lessons.AsNoTracking().Where(lesson => sectionIds.Contains(lesson.ContentSectionId)
                && lesson.ArchiveMode == ContentArchiveMode.None && lesson.Videos.Any(video => video.IsActive && video.ArchiveMode == ContentArchiveMode.None))
            .Select(lesson => new Candidate(CodeType.Lesson, lesson.Id, lesson.Title,
                lesson.ContentSection.IsSystemContainer && lesson.ContentSection.Term.Package.ContentMode == PackageContentMode.SingleLesson ? lesson.ContentSection.Term.Package.Price : lesson.Price,
                lesson.CreatedAt, lesson.ContentSection.Term.PackageId, lesson.ContentSection.TermId, lesson.ContentSectionId,
                lesson.ContentSection.Term.Package.Name, lesson.ContentSection.Term.IsSystemContainer ? null : lesson.ContentSection.Term.Title,
                lesson.ContentSection.IsSystemContainer ? null : lesson.ContentSection.Title)).ToListAsync(ct));
        return candidates.Where(candidate => candidate.Price > 0 && candidate.Price <= available).ToList();
    }

    private static StudentFacingScopeOwnerType OwnerType(CodeType type) => type switch
    {
        CodeType.Package => StudentFacingScopeOwnerType.Package,
        CodeType.Term => StudentFacingScopeOwnerType.Term,
        CodeType.Month => StudentFacingScopeOwnerType.ContentSection,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static bool Owned(Candidate candidate, IReadOnlyList<StudentAccessGrant> grants) => grants.Any(grant =>
        grant.GrantType == CodeType.Package && grant.PackageId == candidate.PackageId
        || candidate.TermId.HasValue && grant.GrantType == CodeType.Term && grant.TermId == candidate.TermId
        || candidate.SectionId.HasValue && grant.GrantType == CodeType.Month && grant.ContentSectionId == candidate.SectionId
        || candidate.ContentType == CodeType.Lesson && grant.GrantType == CodeType.Lesson && grant.LessonId == candidate.ContentId);

    private sealed record Candidate(CodeType ContentType, Guid ContentId, string Title, decimal Price, DateTime CreatedAt,
        Guid PackageId, Guid? TermId, Guid? SectionId, string PackageName, string? TermName, string? SectionName)
    {
        public RechargeRecommendationDto ToDto() => new(ContentType, ContentId, Title, Price, PackageName, TermName, SectionName,
            ContentType switch
            {
                CodeType.Package => $"/student/packages/{PackageId}",
                CodeType.Term => $"/student/packages/{PackageId}/terms/{TermId}",
                CodeType.Month => $"/student/packages/{PackageId}/terms/{TermId}/sections/{SectionId}",
                _ => $"/student/packages/{PackageId}/lessons/{ContentId}"
            });
    }
}
