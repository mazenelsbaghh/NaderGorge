using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherPlatformFeeDefaultsTests
{
    [Theory]
    [InlineData(TeacherFinancePreset.Standard, TeacherAgreementScopeType.Lesson, 100, 15)]
    [InlineData(TeacherFinancePreset.SandyAshraf, TeacherAgreementScopeType.Lesson, 100, 12.5)]
    [InlineData(TeacherFinancePreset.Standard, TeacherAgreementScopeType.ContentSection, 400, 60)]
    [InlineData(TeacherFinancePreset.SandyAshraf, TeacherAgreementScopeType.ContentSection, 400, 50)]
    [InlineData(TeacherFinancePreset.Standard, TeacherAgreementScopeType.Term, 400, 100)]
    [InlineData(TeacherFinancePreset.SandyAshraf, TeacherAgreementScopeType.Package, 1000, 250)]
    [InlineData(TeacherFinancePreset.Nader, TeacherAgreementScopeType.ContentSection, 400, 30)]
    [InlineData(TeacherFinancePreset.Nader, TeacherAgreementScopeType.Term, 400, 100)]
    [InlineData(TeacherFinancePreset.Nader, TeacherAgreementScopeType.Package, 1000, 250)]
    public void Defaults_charge_platform_fee_and_leave_remainder_for_teacher(TeacherFinancePreset preset, TeacherAgreementScopeType scope, decimal price, decimal platformFee)
    {
        var rules = TeacherFinanceDefaults.CreateAgreements(Guid.NewGuid(), Guid.NewGuid(), preset, DateTime.UtcNow).ToList();
        foreach (var trigger in new[] { TeacherAgreementTrigger.ContentSale, TeacherAgreementTrigger.CodeActivation })
        {
            var rule = Assert.Single(rules, x => x.ScopeType == scope && x.Trigger == trigger);
            var agreement = new TeacherAgreementResolution(rule.Id, rule.ScopeType, null, rule.AllocationMode, rule.AllocationValue, rule.PriceBasis);
            var sale = TeacherAgreementResolver.CalculateAllocation(agreement, price, price);
            Assert.Equal(platformFee, price - sale.TeacherShare);
            var batch = TeacherAgreementResolver.CalculateAllocation(agreement, price * 3, price * 3, 3);
            Assert.Equal(platformFee * 3, price * 3 - batch.TeacherShare);
        }
        if (preset == TeacherFinancePreset.Nader) Assert.DoesNotContain(rules, x => x.Trigger == TeacherAgreementTrigger.CodeDelivery);
    }

    [Fact]
    public async Task Consumed_code_locks_batch_terms_without_needing_a_batch_billing_marker()
    {
        await using var db = TestAppDbContextFactory.Create();
        var group = new CodeGroup { Name = "Used codes", CodeType = CodeType.Lesson, AccountingTiming = CodeAccountingTiming.OnActivation };
        db.Add(group);
        db.Add(new AccessCode { CodeGroupId = group.Id, IsConsumed = true });
        await db.SaveChangesAsync();
        var response = await new SetCodeGroupFinancialTermsCommandHandler(db).Handle(new(Guid.NewGuid(), group.Id,
            TeacherAgreementTrigger.CodeDelivery, null, null), CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, response.Status);
        Assert.Null(group.AccountingRecordedAt);
        Assert.Equal(CodeAccountingTiming.OnActivation, group.AccountingTiming);
        Assert.Empty(await db.CodeGroupFinancialTerms.ToListAsync());
    }

    [Fact]
    public async Task Nader_codes_reject_delivery_billing_before_any_code_is_used()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = new TeacherProfile { FinancePreset = TeacherFinancePreset.Nader };
        var group = new CodeGroup { Name = "Nader", TeacherId = teacher.Id, CodeType = CodeType.Term };
        db.AddRange(teacher, group);
        await db.SaveChangesAsync();
        var response = await new SetCodeGroupFinancialTermsCommandHandler(db).Handle(new(Guid.NewGuid(), group.Id,
            TeacherAgreementTrigger.CodeDelivery, null, null), CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Invalid, response.Status);
        Assert.Empty(await db.CodeGroupFinancialTerms.ToListAsync());
    }

    [Fact]
    public async Task Legacy_batch_billing_evidence_locks_terms_even_when_timestamp_is_missing()
    {
        await using var db = TestAppDbContextFactory.Create();
        var group = new CodeGroup { Name = "Legacy", CodeType = CodeType.Package };
        db.AddRange(group, new TeacherFinancialEvent { SourceType = TeacherFinancialSourceType.AccessCodeGeneration, SourceId = group.Id, IdempotencyKey = "old-generation" });
        await db.SaveChangesAsync();
        var response = await new SetCodeGroupFinancialTermsCommandHandler(db).Handle(new(Guid.NewGuid(), group.Id,
            TeacherAgreementTrigger.CodeActivation, null, null), CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, response.Status);
        Assert.Empty(await db.CodeGroupFinancialTerms.ToListAsync());
    }
}
