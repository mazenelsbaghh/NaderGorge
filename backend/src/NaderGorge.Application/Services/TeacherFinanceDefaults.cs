using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Services;

public static class TeacherFinanceDefaults
{
    public static IEnumerable<TeacherFinancialAgreement> CreateAgreements(Guid teacherId, Guid actorId, TeacherFinancePreset preset, DateTime effectiveFrom)
    {
        var lessonFee = preset == TeacherFinancePreset.SandyAshraf ? 12.5m : 15m;
        var monthFee = preset switch { TeacherFinancePreset.Nader => 30m, TeacherFinancePreset.SandyAshraf => 50m, _ => 60m };
        foreach (var trigger in new[] { TeacherAgreementTrigger.AllSources })
        {
            foreach (var scope in new[] { TeacherAgreementScopeType.Lesson, TeacherAgreementScopeType.LessonVideo,
                TeacherAgreementScopeType.ContentSection, TeacherAgreementScopeType.Term, TeacherAgreementScopeType.Package })
            {
                var fixedFee = scope is TeacherAgreementScopeType.Lesson or TeacherAgreementScopeType.LessonVideo ? lessonFee
                    : scope == TeacherAgreementScopeType.ContentSection ? monthFee
                    : preset == TeacherFinancePreset.Nader ? (scope == TeacherAgreementScopeType.Term ? 100m : 250m) : (decimal?)null;
                yield return new TeacherFinancialAgreement { TeacherId = teacherId, CreatedByUserId = actorId,
                    ScopeType = scope, Trigger = trigger, AllocationMode = fixedFee.HasValue ? TeacherAgreementAllocationMode.PlatformFixedPerUnit : TeacherAgreementAllocationMode.Percentage,
                    AllocationValue = fixedFee ?? 75m, PriceBasis = TeacherPriceBasis.NetAfterDiscount,
                    EffectiveFrom = effectiveFrom, Reason = "القواعد الافتراضية لنصيب المنصة — " + preset };
            }
        }
    }
}
