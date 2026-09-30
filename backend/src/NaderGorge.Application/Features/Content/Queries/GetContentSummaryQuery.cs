using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public sealed record GetContentSummaryQuery(
    Guid? TeacherUserId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? TeacherId = null) : IRequest<ApiResponse<ContentSummaryDto>>;

public sealed record ContentAcquisitionCountDto(int Purchased, int Gifts, int RefundedStudents);

public sealed record ContentSummaryNodeDto(
    Guid Id,
    string Title,
    string Kind,
    ContentAcquisitionCountDto Counts,
    IReadOnlyList<ContentSummaryNodeDto> Children);

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
    int TotalStudents,
    int ActiveStudents,
    int RefundOperations,
    IReadOnlyList<ContentSummaryNodeDto> Breakdown);

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
        var allGrants = await _factSource.LoadAsync(
            new ContentGrantFactScope(packageIds, request.FromUtc, request.ToUtc, IncludeCancelled: true),
            ct);
        var grantsByPackage = allGrants.GroupBy(grant => grant.PackageId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var grants = allGrants.Where(grant => !grant.CancelledAt.HasValue).ToArray();
        var refundOperationsByGrant = await new ContentRefundFactSource(_db)
            .LoadOperationIdsByGrantAsync(allGrants, ct);
        var currentTime = DateTime.UtcNow;
        var activeGrants = ContentAcquisitionCalculator.WhereEffectiveAt(grants, currentTime).ToArray();
        var acquisitionsByPackage = ContentAcquisitionCalculator.SummarizePackages(packageIds, activeGrants);
        var terms = await _db.Terms.AsNoTracking()
            .Where(term => packageIds.Contains(term.PackageId))
            .Select(term => new TermRow(term.Id, term.PackageId, term.Title, term.Order, term.IsSystemContainer))
            .ToArrayAsync(ct);
        var termIds = terms.Select(term => term.Id).ToArray();
        var sections = await _db.ContentSections.AsNoTracking()
            .Where(section => termIds.Contains(section.TermId))
            .Select(section => new SectionRow(section.Id, section.TermId, section.Title, section.Order, section.IsSystemContainer))
            .ToArrayAsync(ct);
        var sectionIds = sections.Select(section => section.Id).ToArray();
        var lessons = await _db.Lessons.AsNoTracking()
            .Where(lesson => sectionIds.Contains(lesson.ContentSectionId))
            .Select(lesson => new LessonRow(lesson.Id, lesson.ContentSectionId, lesson.Title, lesson.Order))
            .ToArrayAsync(ct);
        var termsByPackage = terms.GroupBy(term => term.PackageId)
            .ToDictionary(group => group.Key, group => group.OrderBy(term => term.Order).ThenBy(term => term.Title).ToArray());
        var sectionsByTerm = sections.GroupBy(section => section.TermId)
            .ToDictionary(group => group.Key, group => group.OrderBy(section => section.Order).ThenBy(section => section.Title).ToArray());
        var lessonsBySection = lessons.GroupBy(lesson => lesson.SectionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(lesson => lesson.Order).ThenBy(lesson => lesson.Title).ToArray());

        ContentAcquisitionCountDto Counts(IEnumerable<ContentGrantFact> directFacts)
        {
            var facts = directFacts.ToArray();
            var acquisitions = ContentAcquisitionCalculator.SummarizeStudents(
                ContentAcquisitionCalculator.WhereEffectiveAt(facts, currentTime));
            var refundedStudents = facts.Where(fact =>
                    refundOperationsByGrant.TryGetValue(fact.GrantId, out var operations) && operations.Count > 0)
                .Select(fact => fact.UserId).Distinct().Count();
            return new ContentAcquisitionCountDto(acquisitions.Purchased, acquisitions.GiftOnly, refundedStudents);
        }

        int RefundOperations(IEnumerable<ContentGrantFact> facts) =>
            facts.SelectMany(fact => refundOperationsByGrant.GetValueOrDefault(fact.GrantId) ?? [])
                .Distinct().Count();

        var factsByTarget = allGrants.Where(fact => fact.TargetId != Guid.Empty)
            .GroupBy(fact => (fact.GrantType, fact.TargetId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        ContentSummaryNodeDto Node(Guid id, string title, string kind, CodeType grantType, IReadOnlyList<ContentSummaryNodeDto> children) =>
            new(id, title, kind, Counts(factsByTarget.GetValueOrDefault((grantType, id)) ?? []), children);

        IReadOnlyList<ContentSummaryNodeDto> Breakdown(Guid packageId)
        {
            var result = new List<ContentSummaryNodeDto>();
            foreach (var term in termsByPackage.GetValueOrDefault(packageId) ?? [])
            {
                var termChildren = new List<ContentSummaryNodeDto>();
                foreach (var section in sectionsByTerm.GetValueOrDefault(term.Id) ?? [])
                {
                    var lessonChildren = (lessonsBySection.GetValueOrDefault(section.Id) ?? [])
                        .Select(lesson => Node(lesson.Id, lesson.Title, "lesson", CodeType.Lesson, []))
                        .ToArray();
                    if (section.IsSystemContainer) termChildren.AddRange(lessonChildren);
                    else termChildren.Add(Node(section.Id, section.Title, "section", CodeType.Month, lessonChildren));
                }
                if (term.IsSystemContainer) result.AddRange(termChildren);
                else result.Add(Node(term.Id, term.Title, "term", CodeType.Term, termChildren));
            }
            return result;
        }

        var summaries = packages.Select(package =>
        {
            var acquisitions = acquisitionsByPackage[package.Id];
            var packageGrants = grantsByPackage.GetValueOrDefault(package.Id) ?? [];

            return new ContentPackageSummaryDto(
                package.Id,
                package.Name,
                package.TeacherName,
                ToDto(acquisitions.Package, Counts(packageGrants.Where(grant => grant.GrantType == CodeType.Package)).RefundedStudents),
                ToDto(acquisitions.Term, Counts(packageGrants.Where(grant => grant.GrantType == CodeType.Term)).RefundedStudents),
                ToDto(acquisitions.Section, Counts(packageGrants.Where(grant => grant.GrantType == CodeType.Month)).RefundedStudents),
                ToDto(acquisitions.Lesson, Counts(packageGrants.Where(grant => grant.GrantType == CodeType.Lesson)).RefundedStudents),
                acquisitions.Overall.Purchased,
                acquisitions.Overall.GiftOnly,
                acquisitions.Overall.Total,
                acquisitions.Overall.Total,
                RefundOperations(packageGrants),
                Breakdown(package.Id));
        }).ToArray();

        var packageNames = packages.ToDictionary(package => package.Id, package => package.Name);
        var combinations = activeGrants
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

    private static ContentAcquisitionCountDto ToDto(ContentAcquisitionStudentCounts counts, int refundedStudents) =>
        new(counts.Purchased, counts.GiftOnly, refundedStudents);

    private sealed record PackageRow(Guid Id, string Name, string TeacherName);
    private sealed record TermRow(Guid Id, Guid PackageId, string Title, int Order, bool IsSystemContainer);
    private sealed record SectionRow(Guid Id, Guid TermId, string Title, int Order, bool IsSystemContainer);
    private sealed record LessonRow(Guid Id, Guid SectionId, string Title, int Order);
}
