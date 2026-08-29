using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

/// <summary>Owns the one-off, audited financial trigger for a delivered code batch.</summary>
public sealed class CodeGroupFinancialAccountingService
{
    private readonly IAppDbContext _db;
    private readonly TeacherAccountingService _accounting;
    private readonly TeacherAgreementResolver _agreements;

    public CodeGroupFinancialAccountingService(IAppDbContext db, TeacherAccountingService accounting, TeacherAgreementResolver agreements)
        => (_db, _accounting, _agreements) = (db, accounting, agreements);

    public async Task RecordDeliveryAsync(CodeGroup group, CodeGroupFinancialTerms terms, DateTime occurredAt, CancellationToken ct)
    {
        if (group.CodeType == CodeType.Balance || !group.TeacherId.HasValue)
            return;

        var (itemPrice, targetType, targetId, contentName) = await ResolvePricingAsync(group, ct);
        var gross = itemPrice * group.TotalCodes;
        var paid = gross * (1m - Math.Clamp(group.DiscountPercentage ?? 0m, 0m, 100m) / 100m);
        var agreement = await ResolveAgreementAsync(group, terms, targetType, targetId, occurredAt, ct);
        var (allocationMode, teacherShare, basis) = TeacherAgreementResolver.CalculateAllocation(
            agreement, gross, paid, group.TotalCodes);

        await _accounting.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.AccessCodeGeneration, group.Id, null, targetType, targetId,
            gross, gross - paid, paid, 0m, paid - teacherShare,
            $"access-code-group-delivery:{group.Id}",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                codeGroupId = group.Id, group.Name, group.CodeType, group.TotalCodes,
                financialTrigger = TeacherAgreementTrigger.CodeDelivery.ToString()
            }),
            occurredAt, TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(group.TeacherId.Value, allocationMode, agreement.AllocationValue,
                    basis, teacherShare, paid - teacherShare, null, null, contentName, null,
                    AgreementId: agreement.AgreementId, AgreementScopeType: agreement.ScopeType,
                    AgreementScopeId: agreement.ScopeId, AgreementAllocationMode: agreement.AllocationMode,
                    PriceBasis: agreement.PriceBasis)
            }), ct);

        group.AccountingRecordedAt = occurredAt;
    }

    private async Task<TeacherAgreementResolution> ResolveAgreementAsync(CodeGroup group, CodeGroupFinancialTerms terms,
        SalesTargetType targetType, Guid targetId, DateTime occurredAt, CancellationToken ct)
    {
        if (terms.AgreementId is Guid agreementId)
        {
            var selected = await _db.TeacherFinancialAgreements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == agreementId
                && x.TeacherId == group.TeacherId && x.IsActive && x.Trigger == TeacherAgreementTrigger.CodeDelivery
                && x.EffectiveFrom <= occurredAt && (x.EffectiveTo == null || x.EffectiveTo >= occurredAt), ct);
            if (selected != null)
                return new(selected.Id, selected.ScopeType, selected.ScopeId, selected.AllocationMode, selected.AllocationValue, selected.PriceBasis);
        }

        var contentScopes = await _agreements.BuildScopesAsync(targetType, targetId, ct);
        return await _agreements.ResolveAsync(group.TeacherId!.Value, TeacherAgreementTrigger.CodeDelivery,
            [(TeacherAgreementScopeType.CodeGroup, group.Id), .. contentScopes], occurredAt, ct);
    }

    private async Task<(decimal Price, SalesTargetType TargetType, Guid TargetId, string Name)> ResolvePricingAsync(CodeGroup group, CancellationToken ct)
    {
        if (group.PackageId is Guid packageId)
        {
            var item = await _db.Packages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == packageId, ct);
            if (item != null) return (item.Price, SalesTargetType.Package, item.Id, item.Name);
        }
        if (group.TermId is Guid termId)
        {
            var item = await _db.Terms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == termId, ct);
            if (item != null) return (item.Price, SalesTargetType.Term, item.Id, item.Title);
        }
        if (group.ContentSectionId is Guid sectionId)
        {
            var item = await _db.ContentSections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sectionId, ct);
            if (item != null) return (item.Price, SalesTargetType.ContentSection, item.Id, item.Title);
        }
        if (group.LessonId is Guid lessonId)
        {
            var item = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(x => x.Id == lessonId, ct);
            if (item != null) return (item.Price, SalesTargetType.Lesson, item.Id, item.Title);
        }
        if (group.PublicExamProductId is Guid productId)
        {
            var item = await _db.PublicExamProducts.AsNoTracking().Include(x => x.Exam).FirstOrDefaultAsync(x => x.Id == productId, ct);
            if (item != null) return (item.Price, SalesTargetType.PublicExam, item.Id, item.Exam.Title);
        }
        return (0m, SalesTargetType.Platform, group.Id, group.Name);
    }
}
