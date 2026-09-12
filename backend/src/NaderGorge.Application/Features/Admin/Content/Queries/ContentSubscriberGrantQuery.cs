using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Content.Queries;

internal static class ContentSubscriberGrantQuery
{
    internal static CodeType? MapContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "package" => CodeType.Package,
        "term" => CodeType.Term,
        "section" => CodeType.Month,
        "lesson" => CodeType.Lesson,
        _ => null
    };

    internal static IQueryable<Guid> BalanceGrantIds(
        IAppDbContext db,
        IQueryable<StudentAccessGrant> grants)
    {
        var targets = grants.Select(grant => new
        {
            grant.Id,
            grant.UserId,
            TargetId = grant.GrantType == CodeType.Package ? grant.PackageId :
                grant.GrantType == CodeType.Term ? grant.TermId :
                grant.GrantType == CodeType.Month ? grant.ContentSectionId : grant.LessonId,
            TargetType = grant.GrantType == CodeType.Package ? SalesTargetType.Package :
                grant.GrantType == CodeType.Term ? SalesTargetType.Term :
                grant.GrantType == CodeType.Month ? SalesTargetType.ContentSection : SalesTargetType.Lesson
        });
        return targets.Where(target => db.SalesFinancialEffects.Any(effect =>
                effect.StudentId == target.UserId && effect.TargetId == target.TargetId &&
                effect.TargetType == target.TargetType &&
                (effect.PaidAmount > 0m || effect.PromotionalAmount > 0m)) ||
            db.BalanceTransactions.Any(transaction =>
                transaction.StudentBalance.UserId == target.UserId &&
                transaction.TransactionType == "ContentPurchase" &&
                transaction.ReferenceId == target.TargetId))
            .Select(target => target.Id);
    }

    internal static IQueryable<StudentAccessGrant> Build(
        IAppDbContext db,
        string contentType,
        Guid contentId,
        string? search)
    {
        var query = DescendantGrants(db, contentType.ToLowerInvariant(), contentId)
            .Where(grant => !grant.CancelledAt.HasValue);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(grant =>
                grant.User.FullName.ToLower().Contains(normalizedSearch) ||
                grant.User.PhoneNumber.Contains(normalizedSearch));
        }

        return query;
    }

    private static IQueryable<StudentAccessGrant> DescendantGrants(
        IAppDbContext db, string contentType, Guid contentId)
    {
        var termIds = db.Terms.Where(term => contentType == "package" && term.PackageId == contentId)
            .Select(term => term.Id);
        var sectionIds = db.ContentSections.Where(section =>
                termIds.Contains(section.TermId) || (contentType == "term" && section.TermId == contentId))
            .Select(section => section.Id);
        var lessonIds = db.Lessons.Where(lesson =>
                sectionIds.Contains(lesson.ContentSectionId) ||
                (contentType == "section" && lesson.ContentSectionId == contentId))
            .Select(lesson => lesson.Id);

        // Follow the content hierarchy: child grants need not store ancestor IDs.
        return db.StudentAccessGrants.AsNoTracking().Where(grant =>
            (grant.GrantType == CodeType.Package && contentType == "package" && grant.PackageId == contentId) ||
            (grant.GrantType == CodeType.Term && ((contentType == "term" && grant.TermId == contentId) || termIds.Contains(grant.TermId!.Value))) ||
            (grant.GrantType == CodeType.Month && ((contentType == "section" && grant.ContentSectionId == contentId) || sectionIds.Contains(grant.ContentSectionId!.Value))) ||
            (grant.GrantType == CodeType.Lesson && ((contentType == "lesson" && grant.LessonId == contentId) || lessonIds.Contains(grant.LessonId!.Value))));
    }

    internal static IQueryable<StudentAccessGrant> RepresentativePerStudent(IQueryable<StudentAccessGrant> query)
    {
        var representativeIds = query
            .GroupBy(grant => grant.UserId)
            .Select(group => group
                .OrderBy(grant => grant.GiftRecipientId.HasValue)
                .ThenByDescending(grant => grant.GrantedAt)
                .ThenByDescending(grant => grant.CreatedAt)
                .ThenByDescending(grant => grant.Id)
                .Select(grant => grant.Id)
                .First());

        return query.Where(grant => representativeIds.Contains(grant.Id));
    }
}
