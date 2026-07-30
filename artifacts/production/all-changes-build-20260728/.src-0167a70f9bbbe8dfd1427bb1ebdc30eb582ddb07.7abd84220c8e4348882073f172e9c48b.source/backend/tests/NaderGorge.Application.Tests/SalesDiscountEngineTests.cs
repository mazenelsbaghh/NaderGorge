using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Student.Commands;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class SalesDiscountEngineTests
{
    [Fact]
    public async Task Preview_AppliesCouponWithoutMakingPriceNegative()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "15301");
        db.DiscountStackingPolicies.Add(new DiscountStackingPolicy
        {
            Name = "Default",
            NormalizedName = "DEFAULT",
            Mode = StackingMode.AllowMultipleWithCap,
            MaxDiscountPercentage = 100,
            IsDefault = true,
            IsActive = true
        });
        db.SalesCoupons.Add(new SalesCoupon
        {
            Code = "FREE153",
            NormalizedCode = "FREE153",
            Name = "Full discount",
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 500,
            TargetType = SalesTargetType.Platform,
            OwnerType = SalesOwnerType.Platform,
            Status = SalesStatus.Active,
            CreatedByUserId = student.Id
        });
        await db.SaveChangesAsync();
        var engine = new DiscountEngine(db);
        var target = new SalesTargetContext(SalesTargetType.Package, Guid.NewGuid(), 120m, null, null, null, null, true, "Package");

        var result = await engine.PreviewAsync(student.Id, target, new DiscountInput(new[] { "FREE153" }, Array.Empty<string>()), Guid.NewGuid());

        Assert.True(result.Success, result.Error);
        Assert.Equal(120m, result.GrossAmount);
        Assert.Equal(120m, result.CouponDiscountAmount);
        Assert.Equal(120m, result.TotalDiscountAmount);
    }

    [Fact]
    public async Task Commit_ConsumesCouponUsageAndPrintableCode()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "15302");
        var printablePlain = "NG-153-PRINT";
        db.DiscountStackingPolicies.Add(new DiscountStackingPolicy
        {
            Name = "Default",
            NormalizedName = "DEFAULT",
            Mode = StackingMode.AllowCouponAndPrintedCode,
            MaxDiscountPercentage = 100,
            IsDefault = true,
            IsActive = true
        });
        db.SalesCoupons.Add(new SalesCoupon
        {
            Code = "TEN153",
            NormalizedCode = "TEN153",
            Name = "Ten percent",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10,
            TargetType = SalesTargetType.Platform,
            OwnerType = SalesOwnerType.Platform,
            Status = SalesStatus.Active,
            CreatedByUserId = student.Id
        });
        var batch = new PrintableCodeBatch
        {
            Name = "Printed",
            Behavior = PrintableCodeBehavior.Discount,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 15,
            TargetType = SalesTargetType.Platform,
            OwnerType = SalesOwnerType.Platform,
            TotalCodes = 1,
            Status = SalesStatus.Active,
            CreatedByUserId = student.Id
        };
        batch.Codes.Add(new PrintableSalesCode
        {
            CodePlaintext = printablePlain,
            CodeHash = DiscountEngine.HashCode(printablePlain),
            SerialNumber = 1,
            QrPayload = printablePlain,
            Status = SalesStatus.Active
        });
        db.PrintableCodeBatches.Add(batch);
        await db.SaveChangesAsync();
        var engine = new DiscountEngine(db);
        var operationId = Guid.NewGuid();
        var target = new SalesTargetContext(SalesTargetType.Lesson, Guid.NewGuid(), 100m, null, null, null, null, true, "Lesson");

        var result = await engine.CommitAsync(student.Id, target, new DiscountInput(new[] { "TEN153" }, new[] { printablePlain }), operationId);
        await db.SaveChangesAsync();

        Assert.True(result.Success, result.Error);
        Assert.Equal(10m, result.CouponDiscountAmount);
        Assert.Equal(15m, result.PrintableCodeDiscountAmount);
        Assert.Single(db.SalesCouponUsages);
        Assert.Single(db.PrintableCodeRedemptions);
        Assert.Equal(1, db.PrintableSalesCodes.Single().UsedCount);
    }

    [Fact]
    public async Task PurchaseAlreadyOwnedContent_DoesNotConsumeCoupon()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "15303");
        var packageSeed = await TestAppDbContextFactory.SeedPackageAsync(db, "Phase 1", price: 100m);
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = packageSeed.PackageId,
            IsActive = true
        });
        db.StudentBalances.Add(new StudentBalance { UserId = student.Id, CurrentBalance = 500m });
        db.SalesCoupons.Add(new SalesCoupon
        {
            Code = "USED153",
            NormalizedCode = "USED153",
            Name = "Should not consume",
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 20,
            TargetType = SalesTargetType.Platform,
            OwnerType = SalesOwnerType.Platform,
            Status = SalesStatus.Active,
            CreatedByUserId = student.Id
        });
        await db.SaveChangesAsync();
        var handler = new PurchaseContentCommandHandler(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            new PromotionalBalanceService(db),
            new SalesTargetResolver(db),
            new DiscountEngine(db));

        var response = await handler.Handle(
            new PurchaseContentCommand(student.Id, CodeType.Package, packageSeed.PackageId, new[] { "USED153" }, Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Empty(db.SalesCouponUsages);
        Assert.Equal(0, db.SalesCoupons.Single().UsedCount);
    }
}
