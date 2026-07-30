using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed record TeacherAgreementResolution(
    Guid? AgreementId,
    TeacherAgreementScopeType ScopeType,
    Guid? ScopeId,
    TeacherAgreementAllocationMode AllocationMode,
    decimal AllocationValue,
    TeacherPriceBasis PriceBasis);

/// <summary>Resolves one active agreement for a teacher without mutating historic ledger rows.</summary>
public class TeacherAgreementResolver
{
    private readonly IAppDbContext _db;

    public TeacherAgreementResolver(IAppDbContext db) => _db = db;

    public async Task<TeacherAgreementResolution> ResolveAsync(
        Guid teacherId,
        TeacherAgreementTrigger trigger,
        IReadOnlyList<(TeacherAgreementScopeType ScopeType, Guid ScopeId)> scopes,
        DateTime occurredAt,
        CancellationToken ct)
    {
        var candidates = await _db.TeacherFinancialAgreements
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId
                && x.IsActive
                && x.Trigger == trigger
                && x.EffectiveFrom <= occurredAt
                && (x.EffectiveTo == null || x.EffectiveTo >= occurredAt))
            .ToListAsync(ct);

        foreach (var scope in scopes)
        {
            var match = candidates
                .Where(x => x.ScopeType == scope.ScopeType && x.ScopeId == scope.ScopeId)
                .OrderByDescending(x => x.EffectiveFrom)
                .FirstOrDefault();
            if (match != null)
            {
                return ToResolution(match);
            }
        }

        var fallback = candidates
            .Where(x => x.ScopeType == TeacherAgreementScopeType.Default && x.ScopeId == null)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault();
        if (fallback != null)
        {
            return ToResolution(fallback);
        }

        var teacherRate = await _db.TeacherProfiles
            .Where(x => x.Id == teacherId)
            .Select(x => (decimal?)x.CommissionRate)
            .FirstOrDefaultAsync(ct) ?? 0m;

        return new TeacherAgreementResolution(
            null,
            TeacherAgreementScopeType.Default,
            null,
            TeacherAgreementAllocationMode.Percentage,
            teacherRate,
            TeacherPriceBasis.Gross);
    }

    private static TeacherAgreementResolution ToResolution(TeacherFinancialAgreement agreement) => new(
        agreement.Id,
        agreement.ScopeType,
        agreement.ScopeId,
        agreement.AllocationMode,
        agreement.AllocationValue,
        agreement.PriceBasis);

    /// <summary>
    /// Builds the deterministic specificity order required by the finance ledger.
    /// The caller supplies the sold target; this method expands its parents so a
    /// lesson can fall back to section, term, package and finally the default.
    /// </summary>
    public async Task<IReadOnlyList<(TeacherAgreementScopeType ScopeType, Guid ScopeId)>> BuildScopesAsync(
        SalesTargetType targetType, Guid targetId, CancellationToken ct)
    {
        var scopes = new List<(TeacherAgreementScopeType, Guid)>();
        switch (targetType)
        {
            case SalesTargetType.Lesson:
            {
                var lesson = await _db.Lessons.AsNoTracking()
                    .Where(x => x.Id == targetId)
                    .Select(x => new { x.Id, x.ContentSectionId, x.ContentSection.TermId, x.ContentSection.Term.PackageId })
                    .FirstOrDefaultAsync(ct);
                if (lesson != null)
                {
                    scopes.Add((TeacherAgreementScopeType.Lesson, lesson.Id));
                    scopes.Add((TeacherAgreementScopeType.ContentSection, lesson.ContentSectionId));
                    scopes.Add((TeacherAgreementScopeType.Term, lesson.TermId));
                    scopes.Add((TeacherAgreementScopeType.Package, lesson.PackageId));
                }
                break;
            }
            case SalesTargetType.ContentSection:
            {
                var section = await _db.ContentSections.AsNoTracking()
                    .Where(x => x.Id == targetId)
                    .Select(x => new { x.Id, x.TermId, x.Term.PackageId })
                    .FirstOrDefaultAsync(ct);
                if (section != null)
                {
                    scopes.Add((TeacherAgreementScopeType.ContentSection, section.Id));
                    scopes.Add((TeacherAgreementScopeType.Term, section.TermId));
                    scopes.Add((TeacherAgreementScopeType.Package, section.PackageId));
                }
                break;
            }
            case SalesTargetType.Term:
            {
                var term = await _db.Terms.AsNoTracking().Where(x => x.Id == targetId)
                    .Select(x => new { x.Id, x.PackageId }).FirstOrDefaultAsync(ct);
                if (term != null)
                {
                    scopes.Add((TeacherAgreementScopeType.Term, term.Id));
                    scopes.Add((TeacherAgreementScopeType.Package, term.PackageId));
                }
                break;
            }
            case SalesTargetType.Package:
                scopes.Add((TeacherAgreementScopeType.Package, targetId));
                break;
            case SalesTargetType.SpecificVideo:
            {
                var video = await _db.LessonVideos.AsNoTracking().Where(x => x.Id == targetId)
                    .Select(x => new { x.Id, x.LessonId, x.Lesson.ContentSectionId, x.Lesson.ContentSection.TermId, x.Lesson.ContentSection.Term.PackageId })
                    .FirstOrDefaultAsync(ct);
                if (video != null)
                {
                    scopes.Add((TeacherAgreementScopeType.LessonVideo, video.Id));
                    scopes.Add((TeacherAgreementScopeType.Lesson, video.LessonId));
                    scopes.Add((TeacherAgreementScopeType.ContentSection, video.ContentSectionId));
                    scopes.Add((TeacherAgreementScopeType.Term, video.TermId));
                    scopes.Add((TeacherAgreementScopeType.Package, video.PackageId));
                }
                break;
            }
            case SalesTargetType.PublicExam:
                scopes.Add((TeacherAgreementScopeType.PublicExam, targetId));
                break;
        }
        return scopes;
    }

    public static (TeacherAllocationMode AllocationMode, decimal TeacherShare, decimal BasisAmount) CalculateAllocation(
        TeacherAgreementResolution agreement, decimal grossAmount, decimal netAfterDiscountAmount, int units = 1)
    {
        var basis = agreement.PriceBasis == TeacherPriceBasis.Gross ? grossAmount : netAfterDiscountAmount;
        basis = Math.Max(0m, basis);
        var share = agreement.AllocationMode switch
        {
            TeacherAgreementAllocationMode.Percentage => Math.Round(basis * agreement.AllocationValue / 100m, 2, MidpointRounding.AwayFromZero),
            TeacherAgreementAllocationMode.FixedPerSale or TeacherAgreementAllocationMode.FixedPerCode => agreement.AllocationValue * Math.Max(1, units),
            TeacherAgreementAllocationMode.FixedPerBatch => agreement.AllocationValue,
            _ => 0m
        };
        return (agreement.AllocationMode == TeacherAgreementAllocationMode.Percentage
                ? TeacherAllocationMode.Percentage : TeacherAllocationMode.FixedAmount,
            Math.Max(0m, share), basis);
    }
}
