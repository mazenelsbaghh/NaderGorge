using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content;

public enum TeacherSubscriberScope
{
    PackageHierarchy,
    DirectVideo,
    DirectExam
}

public sealed record TeacherSubscriberFact(
    Guid StudentId,
    TeacherSubscriberScope Scope,
    CodeType GrantType,
    bool IsGift,
    bool IsActive,
    DateTime? ExpiresAt,
    bool IsExhausted);

public sealed record TeacherSubscriberCounts(int NonGift, int GiftOnly, int Total);

public sealed record TeacherSubscriberScopeCounts(
    TeacherSubscriberCounts Active,
    TeacherSubscriberCounts NonCancelledHistorical);

public sealed record TeacherSubscriberSummary(
    TeacherSubscriberScopeCounts Overall,
    TeacherSubscriberScopeCounts PackageHierarchy,
    TeacherSubscriberScopeCounts DirectVideo,
    TeacherSubscriberScopeCounts DirectExam,
    bool ScopeCountsAreNonAdditive);

/// <summary>
/// Authoritative, read-only subscriber facts for one teacher. Package hierarchy,
/// direct video and direct exam grants are kept separate so callers cannot add
/// overlapping student counts.
/// </summary>
public sealed class TeacherSubscriberFactSource
{
    private readonly IAppDbContext _db;

    public TeacherSubscriberFactSource(IAppDbContext db) => _db = db;

    public async Task<TeacherSubscriberSummary> SummarizeAsync(
        Guid teacherId,
        DateTime asOfUtc,
        CancellationToken ct)
    {
        var aggregate = await BuildSummaryQuery(teacherId, asOfUtc)
            .SingleOrDefaultAsync(ct);
        return new(
            Map(aggregate?.OverallHistoricalTotal, aggregate?.OverallHistoricalNonGift, aggregate?.OverallActiveTotal, aggregate?.OverallActiveNonGift),
            Map(aggregate?.HierarchyHistoricalTotal, aggregate?.HierarchyHistoricalNonGift, aggregate?.HierarchyActiveTotal, aggregate?.HierarchyActiveNonGift),
            Map(aggregate?.VideoHistoricalTotal, aggregate?.VideoHistoricalNonGift, aggregate?.VideoActiveTotal, aggregate?.VideoActiveNonGift),
            Map(aggregate?.ExamHistoricalTotal, aggregate?.ExamHistoricalNonGift, aggregate?.ExamActiveTotal, aggregate?.ExamActiveNonGift),
            true);
    }

    internal IQueryable<SummaryAggregateRow> BuildSummaryQuery(Guid teacherId, DateTime asOfUtc) =>
        BuildQuery(teacherId, null)
            .GroupBy(_ => 1)
            .Select(group => new SummaryAggregateRow
            {
                OverallHistoricalTotal = group.Select(fact => fact.StudentId).Distinct().Count(),
                OverallHistoricalNonGift = group.Where(fact => !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count(),
                OverallActiveTotal = group.Where(fact => fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc)).Select(fact => fact.StudentId).Distinct().Count(),
                OverallActiveNonGift = group.Where(fact => fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc) && !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count(),
                HierarchyHistoricalTotal = group.Where(fact => fact.Scope == TeacherSubscriberScope.PackageHierarchy).Select(fact => fact.StudentId).Distinct().Count(),
                HierarchyHistoricalNonGift = group.Where(fact => fact.Scope == TeacherSubscriberScope.PackageHierarchy && !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count(),
                HierarchyActiveTotal = group.Where(fact => fact.Scope == TeacherSubscriberScope.PackageHierarchy && fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc)).Select(fact => fact.StudentId).Distinct().Count(),
                HierarchyActiveNonGift = group.Where(fact => fact.Scope == TeacherSubscriberScope.PackageHierarchy && fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc) && !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count(),
                VideoHistoricalTotal = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectVideo).Select(fact => fact.StudentId).Distinct().Count(),
                VideoHistoricalNonGift = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectVideo && !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count(),
                VideoActiveTotal = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectVideo && fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc)).Select(fact => fact.StudentId).Distinct().Count(),
                VideoActiveNonGift = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectVideo && fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc) && !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count(),
                ExamHistoricalTotal = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectExam).Select(fact => fact.StudentId).Distinct().Count(),
                ExamHistoricalNonGift = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectExam && !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count(),
                ExamActiveTotal = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectExam && fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc)).Select(fact => fact.StudentId).Distinct().Count(),
                ExamActiveNonGift = group.Where(fact => fact.Scope == TeacherSubscriberScope.DirectExam && fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc) && !fact.IsGift).Select(fact => fact.StudentId).Distinct().Count()
            });

    public async Task<IReadOnlyList<TeacherSubscriberFact>> LoadStudentAsync(
        Guid teacherId,
        Guid studentId,
        CancellationToken ct) =>
        await BuildQuery(teacherId, studentId)
            .Select(fact => new TeacherSubscriberFact(
                fact.StudentId,
                fact.Scope,
                fact.GrantType,
                fact.IsGift,
                fact.IsActive,
                fact.ExpiresAt,
                fact.IsExhausted))
            .ToListAsync(ct);

    private IQueryable<SubscriberQueryRow> BuildQuery(Guid teacherId, Guid? studentId)
    {
        var hierarchy = PackageHierarchyQuery(teacherId, studentId);
        var directVideo = DirectVideoQuery(teacherId, studentId);
        var directExam = DirectExamQuery(teacherId, studentId);
        return hierarchy.Concat(directVideo).Concat(directExam).Distinct();
    }

    private IQueryable<SubscriberQueryRow> PackageHierarchyQuery(
        Guid teacherId,
        Guid? studentId)
    {
        var grants = EligibleGrants(studentId);
        var packageGrants =
            from package in _db.Packages.AsNoTracking()
            where package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.GrantType == CodeType.Package)
                on (Guid?)package.Id equals grant.PackageId
            select grant;
        var termGrants =
            from term in _db.Terms.AsNoTracking()
            where term.Package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.GrantType == CodeType.Term)
                on (Guid?)term.Id equals grant.TermId
            select grant;
        var sectionGrants =
            from section in _db.ContentSections.AsNoTracking()
            where section.Term.Package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.GrantType == CodeType.Month)
                on (Guid?)section.Id equals grant.ContentSectionId
            select grant;
        var lessonGrants =
            from lesson in _db.Lessons.AsNoTracking()
            where lesson.ContentSection.Term.Package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.GrantType == CodeType.Lesson)
                on (Guid?)lesson.Id equals grant.LessonId
            select grant;

        return Project(
                packageGrants.Concat(termGrants).Concat(sectionGrants).Concat(lessonGrants),
                TeacherSubscriberScope.PackageHierarchy,
                false)
            .Distinct();
    }

    private IQueryable<SubscriberQueryRow> DirectVideoQuery(
        Guid teacherId,
        Guid? studentId)
    {
        var grants = EligibleGrants(studentId).Where(grant => grant.GrantType == CodeType.Video);
        var lessonVideoGrants =
            from video in _db.LessonVideos.AsNoTracking()
            where video.Lesson.ContentSection.Term.Package.TeacherId == teacherId
            join grant in grants on (Guid?)video.Id equals grant.LessonVideoId
            select grant;
        var packageVideoTypeGrants =
            from package in _db.Packages.AsNoTracking()
            where package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.VideoTypeId.HasValue)
                on (Guid?)package.Id equals grant.PackageId
            select grant;
        var termVideoTypeGrants =
            from term in _db.Terms.AsNoTracking()
            where term.Package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.VideoTypeId.HasValue)
                on (Guid?)term.Id equals grant.TermId
            select grant;
        var sectionVideoTypeGrants =
            from section in _db.ContentSections.AsNoTracking()
            where section.Term.Package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.VideoTypeId.HasValue)
                on (Guid?)section.Id equals grant.ContentSectionId
            select grant;
        var lessonVideoTypeGrants =
            from lesson in _db.Lessons.AsNoTracking()
            where lesson.ContentSection.Term.Package.TeacherId == teacherId
            join grant in grants.Where(grant => grant.VideoTypeId.HasValue)
                on (Guid?)lesson.Id equals grant.LessonId
            select grant;
        var codeVideoTypeGrants =
            from accessCode in _db.AccessCodes.AsNoTracking()
            where accessCode.CodeGroup.TeacherId == teacherId
            join grant in grants.Where(grant => grant.VideoTypeId.HasValue)
                on (Guid?)accessCode.Id equals grant.AccessCodeId
            select grant;

        return Project(
                lessonVideoGrants
                    .Concat(packageVideoTypeGrants)
                    .Concat(termVideoTypeGrants)
                    .Concat(sectionVideoTypeGrants)
                    .Concat(lessonVideoTypeGrants)
                    .Concat(codeVideoTypeGrants),
                TeacherSubscriberScope.DirectVideo,
                true)
            .Distinct();
    }

    private IQueryable<SubscriberQueryRow> DirectExamQuery(
        Guid teacherId,
        Guid? studentId)
    {
        var grants = EligibleGrants(studentId).Where(grant => grant.GrantType == CodeType.Exam);
        var publicProductGrants =
            from product in _db.PublicExamProducts.AsNoTracking()
            where product.TeacherId == teacherId ||
                  (!product.TeacherId.HasValue && product.Exam.CreatedByTeacherId == teacherId)
            join grant in grants on (Guid?)product.Id equals grant.PublicExamProductId
            select grant;
        var directExamGrants =
            from exam in _db.Exams.AsNoTracking()
            where exam.CreatedByTeacherId == teacherId
            join grant in grants.Where(grant => !grant.PublicExamProductId.HasValue)
                on (Guid?)exam.Id equals grant.ExamId
            select grant;

        return Project(
                publicProductGrants.Concat(directExamGrants),
                TeacherSubscriberScope.DirectExam,
                false)
            .Distinct();
    }

    private IQueryable<StudentAccessGrant> EligibleGrants(Guid? studentId)
    {
        var grants = _db.StudentAccessGrants.AsNoTracking()
            .Where(grant => !grant.CancelledAt.HasValue);
        return studentId.HasValue
            ? grants.Where(grant => grant.UserId == studentId.Value)
            : grants;
    }

    private static IQueryable<SubscriberQueryRow> Project(
        IQueryable<StudentAccessGrant> grants,
        TeacherSubscriberScope scope,
        bool enforceVideoUses) =>
        grants.Select(grant => new SubscriberQueryRow
        {
            GrantId = grant.Id,
            StudentId = grant.UserId,
            Scope = scope,
            GrantType = grant.GrantType,
            IsGift = grant.GiftRecipientId.HasValue,
            IsActive = grant.IsActive,
            ExpiresAt = grant.ExpiresAt,
            IsExhausted = enforceVideoUses &&
                          grant.MaxUses.HasValue &&
                          grant.UsesConsumed >= grant.MaxUses.Value
        });

    private static TeacherSubscriberScopeCounts Map(
        int? historicalTotalValue,
        int? historicalNonGiftValue,
        int? activeTotalValue,
        int? activeNonGiftValue)
    {
        var historicalTotal = historicalTotalValue ?? 0;
        var historicalNonGift = historicalNonGiftValue ?? 0;
        var activeTotal = activeTotalValue ?? 0;
        var activeNonGift = activeNonGiftValue ?? 0;
        return new(
            new(activeNonGift, activeTotal - activeNonGift, activeTotal),
            new(historicalNonGift, historicalTotal - historicalNonGift, historicalTotal));
    }

    internal sealed class SummaryAggregateRow
    {
        public int OverallHistoricalTotal { get; init; }
        public int OverallHistoricalNonGift { get; init; }
        public int OverallActiveTotal { get; init; }
        public int OverallActiveNonGift { get; init; }
        public int HierarchyHistoricalTotal { get; init; }
        public int HierarchyHistoricalNonGift { get; init; }
        public int HierarchyActiveTotal { get; init; }
        public int HierarchyActiveNonGift { get; init; }
        public int VideoHistoricalTotal { get; init; }
        public int VideoHistoricalNonGift { get; init; }
        public int VideoActiveTotal { get; init; }
        public int VideoActiveNonGift { get; init; }
        public int ExamHistoricalTotal { get; init; }
        public int ExamHistoricalNonGift { get; init; }
        public int ExamActiveTotal { get; init; }
        public int ExamActiveNonGift { get; init; }
    }

    private sealed class SubscriberQueryRow
    {
        public Guid GrantId { get; init; }
        public Guid StudentId { get; init; }
        public TeacherSubscriberScope Scope { get; init; }
        public CodeType GrantType { get; init; }
        public bool IsGift { get; init; }
        public bool IsActive { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public bool IsExhausted { get; init; }
    }
}

public static class TeacherSubscriberCalculator
{
    public static TeacherSubscriberSummary Summarize(IEnumerable<TeacherSubscriberFact> facts, DateTime asOfUtc)
    {
        var subscriberFacts = facts.ToArray();
        return new TeacherSubscriberSummary(
            SummarizeScope(subscriberFacts, asOfUtc),
            SummarizeScope(
                subscriberFacts.Where(fact => fact.Scope == TeacherSubscriberScope.PackageHierarchy),
                asOfUtc),
            SummarizeScope(
                subscriberFacts.Where(fact => fact.Scope == TeacherSubscriberScope.DirectVideo),
                asOfUtc),
            SummarizeScope(
                subscriberFacts.Where(fact => fact.Scope == TeacherSubscriberScope.DirectExam),
                asOfUtc),
            true);
    }

    private static TeacherSubscriberScopeCounts SummarizeScope(IEnumerable<TeacherSubscriberFact> facts, DateTime asOfUtc)
    {
        var subscriberFacts = facts.ToArray();
        return new TeacherSubscriberScopeCounts(
            Count(subscriberFacts.Where(fact => IsEffective(fact, asOfUtc))),
            Count(subscriberFacts));
    }

    private static TeacherSubscriberCounts Count(IEnumerable<TeacherSubscriberFact> facts)
    {
        var subscriberFacts = facts.ToArray();
        var nonGiftStudents = subscriberFacts
            .Where(fact => !fact.IsGift)
            .Select(fact => fact.StudentId)
            .ToHashSet();
        var giftOnlyStudents = subscriberFacts
            .Where(fact => fact.IsGift && !nonGiftStudents.Contains(fact.StudentId))
            .Select(fact => fact.StudentId)
            .ToHashSet();
        return new(
            nonGiftStudents.Count,
            giftOnlyStudents.Count,
            nonGiftStudents.Count + giftOnlyStudents.Count);
    }

    private static bool IsEffective(TeacherSubscriberFact fact, DateTime asOfUtc) =>
        fact.IsActive && !fact.IsExhausted && (!fact.ExpiresAt.HasValue || fact.ExpiresAt > asOfUtc);

    public static bool IsEffectiveAt(TeacherSubscriberFact fact, DateTime asOfUtc) =>
        IsEffective(fact, asOfUtc);
}
