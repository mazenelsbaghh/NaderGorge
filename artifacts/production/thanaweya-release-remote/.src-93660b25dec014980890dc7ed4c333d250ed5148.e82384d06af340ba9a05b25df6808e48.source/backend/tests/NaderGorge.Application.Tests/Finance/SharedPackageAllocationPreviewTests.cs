using NaderGorge.Application.Features.Admin.TeacherFinanceCenter.SharedPackages;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class SharedPackageAllocationPreviewTests
{
    [Fact]
    public void Calculate_returns_positive_platform_share_when_teacher_allocations_fit_sale_basis()
    {
        var result = SharedPackageAllocationPreviewService.Calculate(100m,
        [
            new(Guid.NewGuid(), "Teacher A", null, 60m, TeacherAllocationMode.Percentage, 50m),
            new(Guid.NewGuid(), "Teacher B", null, 40m, TeacherAllocationMode.FixedAmount, 20m)
        ]);

        Assert.Equal(50m, result.TotalTeacherShareAmount);
        Assert.Equal(50m, result.PlatformShareAmount);
        Assert.False(result.RequiresLossAcknowledgement);
    }

    [Fact]
    public void Calculate_exposes_negative_platform_share_and_requires_acknowledgement_for_loss()
    {
        var result = SharedPackageAllocationPreviewService.Calculate(100m,
        [
            new(Guid.NewGuid(), "Teacher A", null, 50m, TeacherAllocationMode.FixedAmount, 80m),
            new(Guid.NewGuid(), "Teacher B", null, 50m, TeacherAllocationMode.FixedAmount, 40m)
        ]);

        Assert.Equal(120m, result.TotalTeacherShareAmount);
        Assert.Equal(-20m, result.PlatformShareAmount);
        Assert.True(result.RequiresLossAcknowledgement);
    }
}
