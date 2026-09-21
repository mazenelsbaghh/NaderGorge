using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;

public static class RefundGrantPriceResolver
{
    public static async Task<decimal?> ResolveManualCeilingAsync(
        IAppDbContext db,
        StudentAccessGrant grant,
        CancellationToken ct)
    {
        var directPrice = await ResolveDirectPriceAsync(db, grant, ct);
        if (directPrice > 0m) return directPrice;

        if (grant.GrantType == CodeType.Lesson && grant.ContentSectionId.HasValue)
        {
            var sectionPrice = await db.ContentSections.AsNoTracking()
                .Where(x => x.Id == grant.ContentSectionId.Value)
                .Select(x => (decimal?)x.Price)
                .SingleOrDefaultAsync(ct);
            if (sectionPrice > 0m) return sectionPrice;
        }

        if (grant.GrantType is CodeType.Lesson or CodeType.Month && grant.TermId.HasValue)
        {
            var termPrice = await db.Terms.AsNoTracking()
                .Where(x => x.Id == grant.TermId.Value)
                .Select(x => (decimal?)x.Price)
                .SingleOrDefaultAsync(ct);
            if (termPrice > 0m) return termPrice;
        }

        if (grant.GrantType is CodeType.Lesson or CodeType.Month or CodeType.Term && grant.PackageId.HasValue)
        {
            var packagePrice = await db.Packages.AsNoTracking()
                .Where(x => x.Id == grant.PackageId.Value)
                .Select(x => (decimal?)x.Price)
                .SingleOrDefaultAsync(ct);
            if (packagePrice > 0m) return packagePrice;
        }

        return null;
    }

    private static Task<decimal?> ResolveDirectPriceAsync(
        IAppDbContext db,
        StudentAccessGrant grant,
        CancellationToken ct) => grant.GrantType switch
    {
        CodeType.Package when grant.PackageId.HasValue => db.Packages.AsNoTracking()
            .Where(x => x.Id == grant.PackageId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Term when grant.TermId.HasValue => db.Terms.AsNoTracking()
            .Where(x => x.Id == grant.TermId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Month when grant.ContentSectionId.HasValue => db.ContentSections.AsNoTracking()
            .Where(x => x.Id == grant.ContentSectionId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Lesson when grant.LessonId.HasValue => db.Lessons.AsNoTracking()
            .Where(x => x.Id == grant.LessonId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Exam when grant.PublicExamProductId.HasValue => db.PublicExamProducts.AsNoTracking()
            .Where(x => x.Id == grant.PublicExamProductId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        CodeType.Exam when grant.ExamId.HasValue => db.PublicExamProducts.AsNoTracking()
            .Where(x => x.ExamId == grant.ExamId.Value).Select(x => (decimal?)x.Price).SingleOrDefaultAsync(ct),
        _ => Task.FromResult<decimal?>(null)
    };
}
