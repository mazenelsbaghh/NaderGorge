using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherFinanceAuthorityCommandTests
{
    [Fact]
    public async Task Overlapping_teacher_agreement_is_rejected_without_second_write()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "Agreement Admin", "01093000001");
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Agreement Teacher", "01093000002");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();
        var terms = new TeacherAgreementTerms(TeacherAgreementScopeType.Default, null,
            TeacherAgreementTrigger.ContentSale, TeacherAgreementAllocationMode.Percentage, 60m,
            TeacherPriceBasis.NetAfterDiscount, DateTime.UtcNow.AddDays(-1), null, "standard terms");
        var handler = new CreateTeacherAgreementCommandHandler(db);

        var created = await handler.Handle(new(actor.Id, teacher.Id, terms), CancellationToken.None);
        var duplicate = await handler.Handle(new(actor.Id, teacher.Id, terms), CancellationToken.None);

        Assert.Equal(TeacherFinanceCommandStatus.Success, created.Status);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, duplicate.Status);
        Assert.Single(await db.TeacherFinancialAgreements.ToListAsync());
    }

    [Fact]
    public async Task Code_delivery_terms_update_legacy_timing_and_audit_actor()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "Code Finance Admin", "01093000003");
        var group = new CodeGroup { Id = Guid.NewGuid(), Name = "Teacher codes", CodeType = CodeType.Package,
            CreatedByUserId = actor.Id, AccountingTiming = CodeAccountingTiming.OnActivation };
        db.CodeGroups.Add(group);
        await db.SaveChangesAsync();
        var handler = new SetCodeGroupFinancialTermsCommandHandler(db);

        var response = await handler.Handle(new(actor.Id, group.Id, TeacherAgreementTrigger.CodeDelivery,
            null, "School recipient"), CancellationToken.None);

        Assert.Equal(TeacherFinanceCommandStatus.Success, response.Status);
        var terms = Assert.Single(await db.CodeGroupFinancialTerms.ToListAsync());
        Assert.Equal(actor.Id, terms.UpdatedByUserId);
        Assert.Equal("School recipient", terms.Recipient);
        Assert.Equal(CodeAccountingTiming.Immediate, group.AccountingTiming);
    }
}
