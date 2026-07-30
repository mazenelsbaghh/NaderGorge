using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class CodeBatchFinanceTests
{
    [Fact]
    public async Task RecordDeliveryAsync_is_idempotent_and_credits_only_the_delivered_teacher_batch()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Code Teacher", "01000000011");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = user.Id, CommissionRate = 10m };
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Code batch", price: 100m);
        var agreement = new TeacherFinancialAgreement
        {
            Id = Guid.NewGuid(), TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Default,
            Trigger = TeacherAgreementTrigger.CodeDelivery, AllocationMode = TeacherAgreementAllocationMode.FixedPerCode,
            AllocationValue = 15m, PriceBasis = TeacherPriceBasis.Gross, EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            Reason = "codes", CreatedByUserId = user.Id
        };
        var group = new CodeGroup { Id = Guid.NewGuid(), Name = "July codes", CodeType = CodeType.Package,
            PackageId = packageId, TotalCodes = 3, TeacherId = teacher.Id, CreatedByUserId = user.Id };
        var terms = new CodeGroupFinancialTerms { Id = Guid.NewGuid(), CodeGroupId = group.Id,
            Trigger = TeacherAgreementTrigger.CodeDelivery, AgreementId = agreement.Id, UpdatedByUserId = user.Id };
        db.TeacherProfiles.Add(teacher);
        db.TeacherFinancialAgreements.Add(agreement);
        db.CodeGroups.Add(group);
        db.CodeGroupFinancialTerms.Add(terms);
        await db.SaveChangesAsync();

        var accounting = new TeacherAccountingService(db);
        var service = new CodeGroupFinancialAccountingService(db, accounting, new TeacherAgreementResolver(db));
        await service.RecordDeliveryAsync(group, terms, DateTime.UtcNow, CancellationToken.None);
        await service.RecordDeliveryAsync(group, terms, DateTime.UtcNow, CancellationToken.None);

        var evt = Assert.Single(db.TeacherFinancialEvents);
        Assert.Equal(TeacherFinancialSourceType.AccessCodeGeneration, evt.SourceType);
        var allocation = Assert.Single(db.TeacherFinancialAllocations);
        Assert.Equal(45m, allocation.TeacherShareAmount);
        Assert.Equal(45m, Assert.Single(db.TeacherAccounts).CurrentBalance);
        Assert.NotNull(group.AccountingRecordedAt);
    }

    [Fact]
    public async Task RecordDeliveryAsync_ignores_balance_recharge_groups()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Balance creator", "01000000012");
        var group = new CodeGroup { Id = Guid.NewGuid(), Name = "Wallet recharge", CodeType = CodeType.Balance,
            BalanceAmount = 100m, TotalCodes = 2, CreatedByUserId = user.Id };
        var terms = new CodeGroupFinancialTerms { Id = Guid.NewGuid(), CodeGroupId = group.Id,
            Trigger = TeacherAgreementTrigger.CodeDelivery, UpdatedByUserId = user.Id };
        db.CodeGroups.Add(group);
        db.CodeGroupFinancialTerms.Add(terms);
        await db.SaveChangesAsync();

        var service = new CodeGroupFinancialAccountingService(db, new TeacherAccountingService(db), new TeacherAgreementResolver(db));
        await service.RecordDeliveryAsync(group, terms, DateTime.UtcNow, CancellationToken.None);

        Assert.Empty(db.TeacherFinancialEvents);
        Assert.Empty(db.TeacherFinancialAllocations);
    }
}
