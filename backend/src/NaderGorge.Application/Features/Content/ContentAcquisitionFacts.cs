using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content;

public sealed record ContentGrantFactScope(
    Guid[] PackageIds,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public sealed record ContentGrantFact(
    Guid PackageId,
    Guid UserId,
    CodeType GrantType,
    bool IsGift,
    DateTime GrantedAt,
    bool IsActive,
    DateTime? ExpiresAt);

public sealed record ContentAcquisitionStudentCounts(
    int Purchased,
    int GiftOnly,
    int Total);

public sealed record ContentPackageAcquisitionSummary(
    Guid PackageId,
    ContentAcquisitionStudentCounts Package,
    ContentAcquisitionStudentCounts Term,
    ContentAcquisitionStudentCounts Section,
    ContentAcquisitionStudentCounts Lesson,
    ContentAcquisitionStudentCounts Overall);

public sealed class ContentGrantFactSource
{
    private readonly IAppDbContext _db;

    public ContentGrantFactSource(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ContentGrantFact>> LoadAsync(
        ContentGrantFactScope scope,
        CancellationToken ct)
    {
        if (scope.PackageIds.Length == 0)
            return [];

        var facts = await PackageFacts(scope).ToListAsync(ct);
        facts.AddRange(await TermFacts(scope).ToListAsync(ct));
        facts.AddRange(await SectionFacts(scope).ToListAsync(ct));
        facts.AddRange(await LessonFacts(scope).ToListAsync(ct));
        return facts;
    }

    private IQueryable<ContentGrantFact> PackageFacts(ContentGrantFactScope scope) =>
        EligibleGrants(scope)
            .Where(grant =>
                grant.GrantType == CodeType.Package &&
                grant.PackageId.HasValue &&
                scope.PackageIds.Contains(grant.PackageId.Value))
            .Select(grant => new ContentGrantFact(
                grant.PackageId!.Value,
                grant.UserId,
                grant.GrantType,
                grant.GiftRecipientId.HasValue,
                grant.GrantedAt,
                grant.IsActive,
                grant.ExpiresAt));

    private IQueryable<ContentGrantFact> TermFacts(ContentGrantFactScope scope) =>
        from grant in EligibleGrants(scope)
        join term in _db.Terms.AsNoTracking() on grant.TermId equals term.Id
        where grant.GrantType == CodeType.Term &&
              scope.PackageIds.Contains(term.PackageId)
        select new ContentGrantFact(
            term.PackageId,
            grant.UserId,
            grant.GrantType,
            grant.GiftRecipientId.HasValue,
            grant.GrantedAt,
            grant.IsActive,
            grant.ExpiresAt);

    private IQueryable<ContentGrantFact> SectionFacts(ContentGrantFactScope scope) =>
        from grant in EligibleGrants(scope)
        join section in _db.ContentSections.AsNoTracking() on grant.ContentSectionId equals section.Id
        join term in _db.Terms.AsNoTracking() on section.TermId equals term.Id
        where grant.GrantType == CodeType.Month &&
              scope.PackageIds.Contains(term.PackageId)
        select new ContentGrantFact(
            term.PackageId,
            grant.UserId,
            grant.GrantType,
            grant.GiftRecipientId.HasValue,
            grant.GrantedAt,
            grant.IsActive,
            grant.ExpiresAt);

    private IQueryable<ContentGrantFact> LessonFacts(ContentGrantFactScope scope) =>
        from grant in EligibleGrants(scope)
        join lesson in _db.Lessons.AsNoTracking() on grant.LessonId equals lesson.Id
        join section in _db.ContentSections.AsNoTracking() on lesson.ContentSectionId equals section.Id
        join term in _db.Terms.AsNoTracking() on section.TermId equals term.Id
        where grant.GrantType == CodeType.Lesson &&
              scope.PackageIds.Contains(term.PackageId)
        select new ContentGrantFact(
            term.PackageId,
            grant.UserId,
            grant.GrantType,
            grant.GiftRecipientId.HasValue,
            grant.GrantedAt,
            grant.IsActive,
            grant.ExpiresAt);

    private IQueryable<StudentAccessGrant> EligibleGrants(ContentGrantFactScope scope)
    {
        var query = _db.StudentAccessGrants.AsNoTracking()
            .Where(grant => !grant.CancelledAt.HasValue);
        if (scope.FromUtc.HasValue)
            query = query.Where(grant => grant.GrantedAt >= scope.FromUtc.Value);
        if (scope.ToUtc.HasValue)
            query = query.Where(grant => grant.GrantedAt < scope.ToUtc.Value);
        return query;
    }
}

public static class ContentAcquisitionCalculator
{
    public static IReadOnlyDictionary<Guid, ContentPackageAcquisitionSummary> SummarizePackages(
        IEnumerable<Guid> packageIds,
        IEnumerable<ContentGrantFact> facts)
    {
        var factsByPackage = facts
            .GroupBy(fact => fact.PackageId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return packageIds.ToDictionary(
            packageId => packageId,
            packageId => SummarizePackage(
                packageId,
                factsByPackage.GetValueOrDefault(packageId) ?? []));
    }

    public static ContentAcquisitionStudentCounts SummarizeStudents(
        IEnumerable<ContentGrantFact> facts)
    {
        var factRows = facts.ToArray();
        var purchasedStudentIds = factRows
            .Where(fact => !fact.IsGift)
            .Select(fact => fact.UserId)
            .ToHashSet();
        var giftOnlyStudentIds = factRows
            .Where(fact => fact.IsGift && !purchasedStudentIds.Contains(fact.UserId))
            .Select(fact => fact.UserId)
            .ToHashSet();

        return new ContentAcquisitionStudentCounts(
            purchasedStudentIds.Count,
            giftOnlyStudentIds.Count,
            purchasedStudentIds.Count + giftOnlyStudentIds.Count);
    }

    public static int CountActiveStudents(
        IEnumerable<ContentGrantFact> facts,
        DateTime asOfUtc) =>
        WhereEffectiveAt(facts, asOfUtc)
            .Select(fact => fact.UserId)
            .Distinct()
            .Count();

    public static IEnumerable<ContentGrantFact> WhereEffectiveAt(
        IEnumerable<ContentGrantFact> facts,
        DateTime asOfUtc) =>
        facts.Where(fact =>
            fact.IsActive &&
            (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc));

    private static ContentPackageAcquisitionSummary SummarizePackage(
        Guid packageId,
        IReadOnlyCollection<ContentGrantFact> facts) =>
        new(
            packageId,
            SummarizeStudents(facts.Where(fact => fact.GrantType == CodeType.Package)),
            SummarizeStudents(facts.Where(fact => fact.GrantType == CodeType.Term)),
            SummarizeStudents(facts.Where(fact => fact.GrantType == CodeType.Month)),
            SummarizeStudents(facts.Where(fact => fact.GrantType == CodeType.Lesson)),
            SummarizeStudents(facts));
}
