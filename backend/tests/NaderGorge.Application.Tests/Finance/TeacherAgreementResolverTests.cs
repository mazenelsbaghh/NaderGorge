using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherAgreementResolverTests
{
    [Fact]
    public async Task ResolveAsync_prefers_exact_parent_then_closest_aggregate_before_default()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Aggregate Teacher", "01000000000");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = user.Id, CommissionRate = 10m };
        var packageId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.TeacherProfiles.Add(teacher);
        db.TeacherFinancialAgreements.AddRange(
            new TeacherFinancialAgreement { Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Default, ScopeId = null, Trigger = TeacherAgreementTrigger.ContentSale, AllocationMode = TeacherAgreementAllocationMode.Percentage, AllocationValue = 20m, PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = now.AddDays(-1), Reason = "everything", CreatedByUserId = user.Id },
            new TeacherFinancialAgreement { Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Package, ScopeId = null, Trigger = TeacherAgreementTrigger.ContentSale, AllocationMode = TeacherAgreementAllocationMode.Percentage, AllocationValue = 30m, PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = now.AddDays(-1), Reason = "all courses", CreatedByUserId = user.Id },
            new TeacherFinancialAgreement { Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Lesson, ScopeId = null, Trigger = TeacherAgreementTrigger.ContentSale, AllocationMode = TeacherAgreementAllocationMode.Percentage, AllocationValue = 40m, PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = now.AddDays(-1), Reason = "all lessons", CreatedByUserId = user.Id },
            new TeacherFinancialAgreement { Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Package, ScopeId = packageId, Trigger = TeacherAgreementTrigger.ContentSale, AllocationMode = TeacherAgreementAllocationMode.Percentage, AllocationValue = 50m, PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = now.AddDays(-1), Reason = "one course", CreatedByUserId = user.Id });
        await db.SaveChangesAsync();

        var resolver = new TeacherAgreementResolver(db);
        var exactParent = await resolver.ResolveAsync(teacher.Id, TeacherAgreementTrigger.ContentSale,
            [(TeacherAgreementScopeType.Lesson, lessonId), (TeacherAgreementScopeType.Package, packageId)], now, CancellationToken.None);
        var aggregateLesson = await resolver.ResolveAsync(teacher.Id, TeacherAgreementTrigger.ContentSale,
            [(TeacherAgreementScopeType.Lesson, Guid.NewGuid()), (TeacherAgreementScopeType.Package, Guid.NewGuid())], now, CancellationToken.None);
        var defaultOnly = await resolver.ResolveAsync(teacher.Id, TeacherAgreementTrigger.ContentSale,
            [(TeacherAgreementScopeType.PublicExam, Guid.NewGuid())], now, CancellationToken.None);

        Assert.Equal(50m, exactParent.AllocationValue);
        Assert.Equal(packageId, exactParent.ScopeId);
        Assert.Equal(40m, aggregateLesson.AllocationValue);
        Assert.Null(aggregateLesson.ScopeId);
        Assert.Equal(20m, defaultOnly.AllocationValue);
        Assert.Equal(TeacherAgreementScopeType.Default, defaultOnly.ScopeType);
    }

    [Fact]
    public async Task ResolveAsync_prefers_lesson_over_package_then_snapshots_the_selected_terms()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01000000001");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = user.Id, CommissionRate = 30m };
        var packageId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.TeacherProfiles.Add(teacher);
        db.TeacherFinancialAgreements.AddRange(
            new TeacherFinancialAgreement { Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Package, ScopeId = packageId, Trigger = TeacherAgreementTrigger.ContentSale, AllocationMode = TeacherAgreementAllocationMode.Percentage, AllocationValue = 35m, PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = now.AddDays(-1), Reason = "package", CreatedByUserId = user.Id },
            new TeacherFinancialAgreement { Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Lesson, ScopeId = lessonId, Trigger = TeacherAgreementTrigger.ContentSale, AllocationMode = TeacherAgreementAllocationMode.FixedPerSale, AllocationValue = 60m, PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = now.AddDays(-1), Reason = "lesson", CreatedByUserId = user.Id });
        await db.SaveChangesAsync();

        var result = await new TeacherAgreementResolver(db).ResolveAsync(teacher.Id, TeacherAgreementTrigger.ContentSale,
            [(TeacherAgreementScopeType.Lesson, lessonId), (TeacherAgreementScopeType.Package, packageId)], now, CancellationToken.None);

        Assert.Equal(TeacherAgreementScopeType.Lesson, result.ScopeType);
        Assert.Equal(TeacherAgreementAllocationMode.FixedPerSale, result.AllocationMode);
        Assert.Equal(60m, result.AllocationValue);
    }

    [Fact]
    public async Task ResolveAsync_falls_back_to_legacy_commission_rate_when_no_agreement_exists()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01000000002");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = user.Id, CommissionRate = 30m };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();

        var result = await new TeacherAgreementResolver(db).ResolveAsync(teacher.Id, TeacherAgreementTrigger.ContentSale, [], DateTime.UtcNow, CancellationToken.None);

        Assert.Null(result.AgreementId);
        Assert.Equal(TeacherAgreementAllocationMode.Percentage, result.AllocationMode);
        Assert.Equal(30m, result.AllocationValue);
    }

    [Fact]
    public async Task ResolveAsync_ignores_expired_override_and_keeps_historic_allocation_snapshot_unchanged()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Agreement Teacher", "01000000003");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = user.Id, CommissionRate = 20m };
        var packageId = Guid.NewGuid();
        var expired = new TeacherFinancialAgreement
        {
            Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Package,
            ScopeId = packageId, Trigger = TeacherAgreementTrigger.ContentSale,
            AllocationMode = TeacherAgreementAllocationMode.FixedPerSale, AllocationValue = 60m,
            PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = DateTime.UtcNow.AddDays(-10),
            EffectiveTo = DateTime.UtcNow.AddDays(-1), Reason = "expired", CreatedByUserId = user.Id
        };
        db.TeacherProfiles.Add(teacher);
        db.TeacherFinancialAgreements.Add(expired);
        await db.SaveChangesAsync();

        var resolver = new TeacherAgreementResolver(db);
        var resolution = await resolver.ResolveAsync(teacher.Id, TeacherAgreementTrigger.ContentSale,
            [(TeacherAgreementScopeType.Package, packageId)], DateTime.UtcNow, CancellationToken.None);
        Assert.Null(resolution.AgreementId);
        Assert.Equal(20m, resolution.AllocationValue);

        var accounting = new TeacherAccountingService(db);
        await accounting.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase, Guid.NewGuid(), null, SalesTargetType.Package, packageId,
            100m, 0m, 100m, 0m, 40m, "agreement-snapshot", "{}", DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            [new TeacherFinancialAllocationInput(teacher.Id, TeacherAllocationMode.FixedAmount, 60m, 100m, 60m, 40m,
                null, null, "Package", AgreementId: expired.Id, AgreementScopeType: expired.ScopeType,
                AgreementScopeId: expired.ScopeId, AgreementAllocationMode: expired.AllocationMode,
                PriceBasis: expired.PriceBasis)]), CancellationToken.None);

        expired.AllocationValue = 5m;
        await db.SaveChangesAsync();

        var allocation = Assert.Single(db.TeacherFinancialAllocations);
        Assert.Equal(60m, allocation.TeacherShareAmount);
        Assert.Equal(60m, allocation.AllocationValue);
        Assert.Equal(expired.Id, allocation.AgreementId);
    }
}
