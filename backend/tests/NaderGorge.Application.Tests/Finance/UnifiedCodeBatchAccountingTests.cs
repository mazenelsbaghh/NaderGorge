using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

public sealed class UnifiedCodeBatchAccountingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    [InlineData(1500)]
    public async Task Hundred_codes_are_billed_once_and_receipts_only_reduce_the_platform_receivable(decimal paidAtDelivery)
    {
        await using var db = TestAppDbContextFactory.Create();
        var (actor, teacher, group) = await SeedBatchAsync(db);
        var terms = await db.CodeGroupFinancialTerms.SingleAsync();
        Assert.Empty(await db.TeacherFinancialEvents.ToListAsync());
        Assert.Null(group.AccountingRecordedAt);
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01022345678");
        var code = await db.AccessCodes.FirstAsync();
        var activate = new ActivateCodeCommandHandler(db, new FakeJobEnqueuer());
        var premature = await activate.Handle(new(student.Id, code.CodePlaintext), CancellationToken.None);
        Assert.False(premature.Success);
        Assert.False((await db.AccessCodes.SingleAsync(x => x.Id == code.Id)).IsConsumed);

        var posting = new FinancialPostingService(db);
        var handler = new ConfirmCodeGroupDeliveryCommandHandler(db,
            new CodeGroupFinancialAccountingService(db, new TeacherAccountingService(db)), posting);
        var quote = await CodeGroupFinanceQuote.CalculateAsync(db, group, terms, DateTime.UtcNow, CancellationToken.None);
        var treasury = await db.TreasuryAccounts.FirstAsync();
        var command = new ConfirmCodeGroupDeliveryCommand(actor.Id, group.Id, "Teacher", null, null, quote.Key,
            paidAtDelivery > 0 ? new(paidAtDelivery, treasury.Id, "receipt-1", "first-receipt") : null);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await handler.Handle(command, CancellationToken.None)).Status);
        Assert.True((await handler.Handle(command, CancellationToken.None)).AlreadyApplied);
        var deletion = await new RemoveUnusedCodesCommandHandler(db, new NoOpAudit()).Handle(new(group.Id, actor.Id, false), CancellationToken.None);
        Assert.False(deletion.Success);
        Assert.Equal(100, await db.AccessCodes.CountAsync());
        var redeemed = await activate.Handle(new(student.Id, code.CodePlaintext), CancellationToken.None);
        Assert.True(redeemed.Success, redeemed.Message);
        Assert.Single(await db.TeacherFinancialEvents.ToListAsync());
        var allocation = Assert.Single(await db.TeacherFinancialAllocations.ToListAsync());
        Assert.Equal(8500m, allocation.TeacherShareAmount);
        Assert.Equal(TeacherFinancialPayoutStatus.Paid, allocation.PayoutStatus);
        Assert.True(allocation.RetainedByTeacher);
        var account = await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, CancellationToken.None);
        Assert.Equal(8500m, account!.TotalEarned);
        Assert.Equal(8500m, account.Retained);
        Assert.Equal(0m, account.Paid);
        Assert.Equal(0m, account.NetPayable);
        Assert.Equal(1500m - paidAtDelivery, account.CodeAmountDue);
        Assert.Equal(0m, account.BalanceDifference);
        Assert.Equal(0m, account.SourceDifference);

        if (paidAtDelivery < 1500m)
        {
            var collect = new CollectCodeGroupPaymentCommandHandler(db, posting);
            var payment = new CollectCodeGroupPaymentCommand(actor.Id, group.Id,
                new(1500m - paidAtDelivery, treasury.Id, "last-receipt", "last-payment"));
            Assert.Equal(TeacherFinanceCommandStatus.Success, (await collect.Handle(payment, CancellationToken.None)).Status);
            Assert.True((await collect.Handle(payment, CancellationToken.None)).AlreadyApplied);
            Assert.Equal(TeacherFinanceCommandStatus.Conflict, (await collect.Handle(payment with
                { Payment = payment.Payment with { Amount = 1m } }, CancellationToken.None)).Status);
            Assert.Equal(TeacherFinanceCommandStatus.Invalid, (await collect.Handle(payment with
                { Payment = payment.Payment with { Amount = 1m, IdempotencyKey = "overpayment" } }, CancellationToken.None)).Status);
        }
        account = await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, CancellationToken.None);
        Assert.Equal(0m, account!.CodeAmountDue);
        Assert.Equal(1500m, account.CodeAmountCollected);
        Assert.Single(await db.TeacherFinancialEvents.ToListAsync());
        Assert.Equal(1500m, await db.JournalLines.Where(x => x.FinancialAccount.Code == "4000").SumAsync(x => x.Credit - x.Debit));
        Assert.Equal(0m, await db.JournalLines.Where(x => x.FinancialAccount.Code == "1200").SumAsync(x => x.Debit - x.Credit));
        Assert.Equal(0m, await db.JournalLines.Where(x => x.FinancialAccount.Code == "2000").SumAsync(x => x.Credit - x.Debit));
        Assert.Equal(1500m, await db.JournalLines.Where(x => x.FinancialAccount.Role == FinancialAccountRole.Treasury).SumAsync(x => x.Debit - x.Credit));
        var export = await new TeacherFinanceExportService(db).ExportDayAsync(actor.Id, CairoTime.ToLocal(DateTime.UtcNow).Date, CancellationToken.None);
        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        Assert.Equal(10000m, workbook.Worksheet(1).Cell("B3").GetValue<decimal>());
        Assert.Equal(8500m, workbook.Worksheet(1).Cell("E3").GetValue<decimal>());
        Assert.Equal("نصيب محتفظ به من الأكواد", workbook.Worksheet(1).Cell("K6").GetString());
    }

    [Fact]
    public async Task Changed_agreement_requires_a_new_preview_before_delivery()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (actor, _, group) = await SeedBatchAsync(db);
        var terms = await db.CodeGroupFinancialTerms.SingleAsync();
        var quote = await CodeGroupFinanceQuote.CalculateAsync(db, group, terms, DateTime.UtcNow, CancellationToken.None);
        (await db.TeacherFinancialAgreements.SingleAsync()).AllocationValue = 20m;
        await db.SaveChangesAsync();
        var handler = new ConfirmCodeGroupDeliveryCommandHandler(db,
            new CodeGroupFinancialAccountingService(db, new TeacherAccountingService(db)), new FinancialPostingService(db));
        var result = await handler.Handle(new(actor.Id, group.Id, "Teacher", null, null, quote.Key), CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, result.Status);
        Assert.Empty(await db.TeacherFinancialEvents.ToListAsync());
        Assert.Empty(await db.CodeGroupDeliveryConfirmations.ToListAsync());
        Assert.Empty(await db.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task One_agreement_replaces_source_variants_and_applies_to_purchases_activation_and_delivery()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (actor, teacher, group) = await SeedBatchAsync(db);
        db.TeacherFinancialAgreements.RemoveRange(await db.TeacherFinancialAgreements.ToListAsync());
        foreach (var trigger in new[] { TeacherAgreementTrigger.ContentSale, TeacherAgreementTrigger.CodeActivation, TeacherAgreementTrigger.CodeDelivery })
            db.TeacherFinancialAgreements.Add(new() { TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Default,
                Trigger = trigger, AllocationMode = TeacherAgreementAllocationMode.Percentage, AllocationValue = 50m,
                PriceBasis = TeacherPriceBasis.NetAfterDiscount, EffectiveFrom = DateTime.UtcNow.AddDays(-2), Reason = "old" });
        await db.SaveChangesAsync();
        var starts = DateTime.UtcNow;
        var response = await new CreateTeacherAgreementCommandHandler(db).Handle(new(actor.Id, teacher.Id,
            new(TeacherAgreementScopeType.Default, null, TeacherAgreementTrigger.AllSources,
                TeacherAgreementAllocationMode.PlatformFixedPerUnit, 15m, TeacherPriceBasis.NetAfterDiscount,
                starts, null, "one rule")), CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Success, response.Status);
        foreach (var trigger in new[] { TeacherAgreementTrigger.ContentSale, TeacherAgreementTrigger.CodeActivation, TeacherAgreementTrigger.CodeDelivery })
        {
            var resolved = await new TeacherAgreementResolver(db).ResolveAsync(teacher.Id, trigger,
                [(TeacherAgreementScopeType.Package, group.PackageId!.Value)], starts.AddSeconds(1), CancellationToken.None);
            Assert.Equal(response.Id, resolved.AgreementId);
            Assert.Equal(85m, TeacherAgreementResolver.CalculateAllocation(resolved, 100m, 100m).TeacherShare);
        }
        Assert.Equal(4, await db.TeacherFinancialAgreements.CountAsync());
    }

    internal static async Task<(User Actor, TeacherProfile Teacher, CodeGroup Group)> SeedBatchAsync(AppDbContext db)
    {
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01012345678");
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Type == RoleType.Admin);
        if (role == null) { role = new Role { Name = "Admin", Type = RoleType.Admin }; db.Roles.Add(role); }
        db.UserRoles.Add(new() { UserId = actor.Id, RoleId = role.Id });
        var teacher = new TeacherProfile { UserId = actor.Id, CommissionRate = 10m };
        db.TeacherProfiles.Add(teacher);
        var subject = new Subject { Name = "Batch subject", NormalizedName = "BATCH_SUBJECT" };
        var package = new Package { Name = "Batch content", Description = "Test batch", Price = 100m,
            SubjectId = subject.Id, TeacherId = teacher.Id, TargetGrade = "3" };
        db.Subjects.Add(subject); db.Packages.Add(package);
        var packageId = package.Id;
        db.TeacherFinancialAgreements.Add(new() { TeacherId = teacher.Id, ScopeType = TeacherAgreementScopeType.Default,
            Trigger = TeacherAgreementTrigger.AllSources, AllocationMode = TeacherAgreementAllocationMode.PlatformFixedPerUnit,
            AllocationValue = 15m, PriceBasis = TeacherPriceBasis.NetAfterDiscount, EffectiveFrom = DateTime.UtcNow.AddDays(-1), Reason = "one agreement" });
        await db.SaveChangesAsync();
        await PlatformFinanceSeeder.SeedAsync(db);
        var result = await new BulkGenerateCodesCommandHandler(db, new NoOpAudit()).Handle(new("100 codes", CodeType.Package,
            100, 12, actor.Id, PackageId: packageId, AccountingTiming: CodeAccountingTiming.Immediate), CancellationToken.None);
        Assert.True(result.Success, result.Message);
        return (actor, teacher, await db.CodeGroups.SingleAsync());
    }

    private sealed class NoOpAudit : IAuditService
    {
        public Task LogAsync(string action, string entityType, Guid? entityId, Guid? userId, object? oldValues = null,
            object? newValues = null, string? ipAddress = null, string? correlationId = null) => Task.CompletedTask;
    }
}
