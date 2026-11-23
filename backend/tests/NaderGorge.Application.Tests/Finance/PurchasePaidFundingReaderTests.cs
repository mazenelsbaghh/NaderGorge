using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Finance;

public sealed class PurchasePaidFundingReaderTests
{
    [Theory]
    [InlineData(0, 1350, 0, false, 1350)]
    [InlineData(100, 200, 300, false, 300)]
    [InlineData(0, 0, 1350, false, 0)]
    [InlineData(100, 200, 300, true, 300)]
    public async Task Exact_funding_evidence_distinguishes_paid_recharge_from_gifts_without_rewriting_history(
        int general, int recharge, int gift, bool currentFormat, int expectedPaid)
    {
        await using var db = await OpenDatabase();
        var source = await SeedPurchase(db, general, recharge, gift, currentFormat);
        var originalPaid = source.PaidAmount;

        var paid = await new PurchasePaidFundingReader(db).ReadAsync(source, default);
        var replay = await new PurchasePaidFundingReader(db).ReadAsync(source, default);

        Assert.Equal(expectedPaid, paid);
        Assert.Equal(paid, replay);
        Assert.Equal(originalPaid, (await db.SalesFinancialEffects.AsNoTracking().SingleAsync()).PaidAmount);
        Assert.Empty(await db.JournalEntries.ToArrayAsync());
    }

    [Theory]
    [InlineData("student")]
    [InlineData("content")]
    [InlineData("type")]
    [InlineData("teacher")]
    [InlineData("amount")]
    [InlineData("missing-link")]
    [InlineData("conflicting-link")]
    public async Task Unproven_or_conflicting_funding_does_not_turn_promotional_credit_into_cash(string mismatch)
    {
        await using var db = await OpenDatabase();
        var source = await SeedPurchase(db, 0, 100, 0, false);
        var usage = await db.PromotionalBalanceUsages.Include(usage => usage.Allocation).SingleAsync();
        var financialEvent = await db.TeacherFinancialEvents.SingleAsync();
        switch (mismatch)
        {
            case "student": usage.Allocation.StudentId = Guid.NewGuid(); break;
            case "content": usage.ContentId = Guid.NewGuid(); break;
            case "type": usage.ContentType = CodeType.Lesson; break;
            case "teacher": usage.Allocation.TeacherId = Guid.NewGuid(); break;
            case "amount":
                source.PromotionalAmount = source.GrossAmount = 101;
                financialEvent.PromotionalAmount = financialEvent.GrossAmount = 101;
                break;
            case "missing-link": financialEvent.DetailsJson = "{}"; break;
            case "conflicting-link": source.DetailsJson = JsonSerializer.Serialize(new { fundingOperationId = Guid.NewGuid() }); break;
        }
        await db.SaveChangesAsync();

        Assert.Equal(0m, await new PurchasePaidFundingReader(db).ReadAsync(source, default));
    }

    [Fact]
    public async Task Mixed_legacy_purchase_refund_ceiling_includes_paid_recharge_and_excludes_gift_funding()
    {
        await using var db = await OpenDatabase();
        var source = await SeedPurchase(db, 100, 200, 300, false);
        var package = new Package { Id = source.TargetId, Name = "Historical paid package", TargetGrade = "SecondSecondary" };
        var grant = new StudentAccessGrant { UserId = source.StudentId, PackageId = package.Id, GrantType = CodeType.Package };
        db.AddRange(package, grant);
        await db.SaveChangesAsync();

        var preview = await new GetRefundUsagePreviewQueryHandler(db).Handle(new(source.StudentId, grant.Id, source.PurchaseOperationId), default);

        Assert.NotNull(preview);
        Assert.Equal(300m, preview.PaidAmount);
        Assert.Equal(300m, preview.RemainingRefundableAmount);
    }

    [Fact]
    public async Task Already_corrected_sale_is_not_counted_as_paid_twice()
    {
        await using var db = await OpenDatabase();
        var source = await SeedPurchase(db, 100, 200, 300, false);
        source.PaidAmount = 300;
        source.PromotionalAmount = 300;
        await db.SaveChangesAsync();

        Assert.Equal(300m, await new PurchasePaidFundingReader(db).ReadAsync(source, default));
    }

    private static async Task<AppDbContext> OpenDatabase()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:;Foreign Keys=False").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<SalesFinancialEffect> SeedPurchase(AppDbContext db, int general, int recharge, int gift, bool currentFormat)
    {
        var fundingId = Guid.NewGuid();
        var source = new SalesFinancialEffect { PurchaseOperationId = Guid.NewGuid(), StudentId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(), TargetId = Guid.NewGuid(), TargetType = SalesTargetType.Package,
            GrossAmount = general + recharge + gift, PaidAmount = general + (currentFormat ? recharge : 0),
            PromotionalAmount = gift + (currentFormat ? 0 : recharge),
            DetailsJson = currentFormat ? JsonSerializer.Serialize(new { fundingOperationId = fundingId, paidTeacherBalanceAmount = recharge }) : "{}" };
        db.Add(source);
        db.Add(new TeacherFinancialEvent { SourceId = source.PurchaseOperationId, SourceType = TeacherFinancialSourceType.DirectPurchase,
            StudentId = source.StudentId, TargetId = source.TargetId, TargetType = source.TargetType,
            GrossAmount = source.GrossAmount, PaidAmount = source.PaidAmount, PromotionalAmount = source.PromotionalAmount,
            IdempotencyKey = Guid.NewGuid().ToString(), DetailsJson = JsonSerializer.Serialize(new { fundingOperationId = fundingId }) });
        if (recharge > 0) AddUsage(db, source, fundingId, recharge, "DIGITAL_RECHARGE");
        if (gift > 0) AddUsage(db, source, fundingId, gift, "GRANTED");
        await db.SaveChangesAsync();
        return source;
    }

    private static void AddUsage(AppDbContext db, SalesFinancialEffect source, Guid fundingId, int amount, string outcome)
    {
        var issuance = new GiftIssuance { RequestId = Guid.NewGuid(), TargetType = GiftTargetType.TeacherBalance,
            TeacherId = source.TeacherId, Amount = amount, Reason = "Funding fixture", IssuedByUserId = Guid.NewGuid() };
        var recipient = new GiftRecipient { GiftIssuance = issuance, StudentId = source.StudentId, OutcomeCode = outcome };
        var allocation = new PromotionalBalanceAllocation { GiftRecipient = recipient, StudentId = source.StudentId,
            TeacherId = source.TeacherId, OriginalAmount = amount, ConsumedAmount = amount };
        db.Add(new PromotionalBalanceUsage { Allocation = allocation, GiftRecipient = recipient,
            PurchaseOperationId = fundingId, ContentType = CodeType.Package, ContentId = source.TargetId, Amount = amount });
    }
}
