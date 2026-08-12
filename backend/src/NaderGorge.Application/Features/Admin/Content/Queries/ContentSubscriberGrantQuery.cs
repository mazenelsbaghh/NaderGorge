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

    internal static IQueryable<Guid> BalanceStudentIds(
        IAppDbContext db,
        string contentType,
        Guid contentId)
    {
        var salesTargetType = contentType.ToLowerInvariant() switch
        {
            "package" => SalesTargetType.Package,
            "term" => SalesTargetType.Term,
            "section" => SalesTargetType.ContentSection,
            "lesson" => SalesTargetType.Lesson,
            _ => (SalesTargetType?)null
        };

        var financialEffectStudents = salesTargetType.HasValue
            ? db.SalesFinancialEffects.AsNoTracking()
                .Where(effect =>
                    effect.TargetType == salesTargetType.Value &&
                    effect.TargetId == contentId &&
                    (effect.PaidAmount > 0m || effect.PromotionalAmount > 0m))
                .Select(effect => effect.StudentId)
            : db.SalesFinancialEffects.AsNoTracking().Where(_ => false).Select(effect => effect.StudentId);

        var legacyTransactionStudents = db.BalanceTransactions.AsNoTracking()
            .Where(transaction =>
                transaction.TransactionType == "ContentPurchase" &&
                transaction.ReferenceId == contentId)
            .Select(transaction => transaction.StudentBalance.UserId);

        return financialEffectStudents.Concat(legacyTransactionStudents).Distinct();
    }

    internal static IQueryable<StudentAccessGrant> Build(
        IAppDbContext db,
        string contentType,
        Guid contentId,
        string? search)
    {
        var grantType = MapContentType(contentType);
        if (!grantType.HasValue)
            return db.StudentAccessGrants.AsNoTracking().Where(_ => false);

        var query = db.StudentAccessGrants
            .AsNoTracking()
            .Where(grant => grant.GrantType == grantType.Value && !grant.CancelledAt.HasValue);

        query = contentType.ToLowerInvariant() switch
        {
            "package" => query.Where(grant => grant.PackageId == contentId),
            "term" => query.Where(grant => grant.TermId == contentId),
            "section" => query.Where(grant => grant.ContentSectionId == contentId),
            "lesson" => query.Where(grant => grant.LessonId == contentId),
            _ => query.Where(_ => false)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(grant =>
                grant.User.FullName.ToLower().Contains(normalizedSearch) ||
                grant.User.PhoneNumber.Contains(normalizedSearch));
        }

        return query;
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
