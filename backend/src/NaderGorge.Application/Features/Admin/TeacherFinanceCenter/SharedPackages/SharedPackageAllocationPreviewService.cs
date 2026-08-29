using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.TeacherFinanceCenter.SharedPackages;

/// <summary>Calculates the immutable financial view of a shared-package sale before any money moves.</summary>
public static class SharedPackageAllocationPreviewService
{
    public static SharedPackageAllocationPreview Calculate(
        decimal saleBasisAmount,
        IEnumerable<SharedPackageAllocationCandidate> candidates)
    {
        var allocations = candidates.Select(candidate =>
        {
            var teacherShare = candidate.AllocationMode == TeacherAllocationMode.FixedAmount
                ? candidate.AllocationValue
                : Math.Round(candidate.BasisAmount * candidate.AllocationValue / 100m, 2, MidpointRounding.AwayFromZero);

            return new SharedPackageTeacherAllocationPreview(
                candidate.TeacherId,
                candidate.TeacherName,
                candidate.SubjectId,
                candidate.BasisAmount,
                candidate.AllocationMode,
                candidate.AllocationValue,
                teacherShare,
                candidate.AgreementId,
                candidate.AgreementScopeType,
                candidate.AgreementScopeId,
                candidate.AgreementAllocationMode,
                candidate.PriceBasis);
        }).ToList();

        var totalTeacherShare = allocations.Sum(x => x.TeacherShareAmount);
        var platformShare = saleBasisAmount - totalTeacherShare;
        return new SharedPackageAllocationPreview(
            saleBasisAmount,
            totalTeacherShare,
            platformShare,
            platformShare < 0m,
            allocations);
    }
}

public sealed record SharedPackageAllocationCandidate(
    Guid TeacherId,
    string TeacherName,
    Guid? SubjectId,
    decimal BasisAmount,
    TeacherAllocationMode AllocationMode,
    decimal AllocationValue,
    Guid? AgreementId = null,
    TeacherAgreementScopeType? AgreementScopeType = null,
    Guid? AgreementScopeId = null,
    TeacherAgreementAllocationMode? AgreementAllocationMode = null,
    TeacherPriceBasis? PriceBasis = null);

public sealed record SharedPackageTeacherAllocationPreview(
    Guid TeacherId,
    string TeacherName,
    Guid? SubjectId,
    decimal BasisAmount,
    TeacherAllocationMode AllocationMode,
    decimal AllocationValue,
    decimal TeacherShareAmount,
    Guid? AgreementId = null,
    TeacherAgreementScopeType? AgreementScopeType = null,
    Guid? AgreementScopeId = null,
    TeacherAgreementAllocationMode? AgreementAllocationMode = null,
    TeacherPriceBasis? PriceBasis = null);

public sealed record SharedPackageAllocationPreview(
    decimal SaleBasisAmount,
    decimal TotalTeacherShareAmount,
    decimal PlatformShareAmount,
    bool RequiresLossAcknowledgement,
    IReadOnlyList<SharedPackageTeacherAllocationPreview> Allocations);
