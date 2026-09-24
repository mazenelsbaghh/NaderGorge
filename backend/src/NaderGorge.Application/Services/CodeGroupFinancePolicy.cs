using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class CodeGroupFinancePolicy(IAppDbContext db)
{
    private readonly IAppDbContext _db = db;
    private readonly TeacherAgreementResolver _agreements = new(db);

    public static TeacherAgreementTrigger Trigger(CodeGroup group, CodeGroupFinancialTerms? terms) =>
        terms?.Trigger ?? (group.AccountingTiming == CodeAccountingTiming.Immediate
            ? TeacherAgreementTrigger.CodeDelivery : TeacherAgreementTrigger.CodeActivation);

    public async Task<TeacherAgreementResolution> ResolveAgreementAsync(CodeGroup group, CodeGroupFinancialTerms terms,
        SalesTargetType targetType, Guid targetId, TeacherAgreementTrigger trigger, DateTime occurredAt, CancellationToken ct)
    {
        if (terms.AgreementId is Guid agreementId)
        {
            var selected = await _db.TeacherFinancialAgreements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == agreementId
                && x.TeacherId == group.TeacherId && x.IsActive && (x.Trigger == trigger || x.Trigger == TeacherAgreementTrigger.AllSources)
                && x.EffectiveFrom <= occurredAt && (x.EffectiveTo == null || x.EffectiveTo > occurredAt), ct);
            if (selected != null) return new(selected.Id, selected.ScopeType, selected.ScopeId, selected.AllocationMode, selected.AllocationValue, selected.PriceBasis);
        }

        var contentScopes = await _agreements.BuildScopesAsync(targetType, targetId, ct);
        return await _agreements.ResolveAsync(group.TeacherId!.Value, trigger,
            [(TeacherAgreementScopeType.CodeGroup, group.Id), .. contentScopes], occurredAt, ct);
    }

    public async Task<(decimal Price, SalesTargetType TargetType, Guid TargetId, string Name)> ResolvePricingAsync(CodeGroup group, CancellationToken ct)
    {
        Guid? targetId = group.CodeType switch
        {
            CodeType.Package => group.PackageId,
            CodeType.Term => group.TermId,
            CodeType.Month => group.ContentSectionId,
            CodeType.Lesson => group.LessonId,
            CodeType.Exam => group.PublicExamProductId ?? group.ExamId,
            _ => null
        };
        if (group.CodeType == CodeType.Video)
        {
            var targets = await _db.CodeVideoTargets.Where(x => x.CodeGroupId == group.Id).Select(x => x.LessonVideoId).ToListAsync(ct);
            if (targets.Count == 1) targetId = targets[0];
        }
        var target = targetId.HasValue
            ? await new SalesTargetResolver(_db).ResolveFromCodeTypeAsync(group.CodeType, targetId.Value, ct) : null;
        return target == null ? (0m, SalesTargetType.Platform, group.Id, group.Name)
            : (target.Price, target.TargetType, target.TargetId ?? group.Id, target.DisplayName);
    }
}
