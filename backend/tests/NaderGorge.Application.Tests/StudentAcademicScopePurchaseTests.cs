using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class StudentAcademicScopePurchaseTests
{
    [Fact]
    public async Task PurchaseContent_DeniesNonMatchingPackageBeforeBalanceDeductionAndCouponUsage()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db);
        var package = await SeedPackageAsync(db, "Denied package", 100m);
        db.StudentBalances.Add(new StudentBalance { UserId = student.Id, CurrentBalance = 500m });
        db.StudentFacingAcademicScopes.Add(NonMatchingScope(StudentFacingScopeOwnerType.Package, package.Id));
        db.SalesCoupons.Add(new SalesCoupon
        {
            Code = "DENIED159",
            NormalizedCode = "DENIED159",
            Name = "Should not consume",
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 50m,
            TargetType = SalesTargetType.Platform,
            OwnerType = SalesOwnerType.Platform,
            Status = SalesStatus.Active,
            CreatedByUserId = student.Id
        });
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var handler = new PurchaseContentCommandHandler(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            new PromotionalBalanceService(db),
            new SalesTargetResolver(db),
            new DiscountEngine(db, academicScope),
            academicScope: academicScope);

        var result = await handler.Handle(
            new PurchaseContentCommand(student.Id, CodeType.Package, package.Id, ["DENIED159"], []),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", result.Errors ?? []);
        Assert.Equal(500m, db.StudentBalances.Single(x => x.UserId == student.Id).CurrentBalance);
        Assert.Empty(db.BalanceTransactions);
        Assert.Empty(db.StudentAccessGrants);
        Assert.Empty(db.SalesCouponUsages);
        Assert.Equal(0, db.SalesCoupons.Single().UsedCount);
    }

    [Fact]
    public async Task PurchaseContent_DeniesInheritedChildTargetBeforeCreatingGrant()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db);
        var package = await SeedPackageAsync(db, "Parent package", 100m);
        var term = new Term { Title = "Term", PackageId = package.Id, Package = package, Price = 40m };
        var section = new ContentSection { Title = "Section", TermId = term.Id, Term = term, Price = 30m };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary", ContentSectionId = section.Id, ContentSection = section, Price = 20m };
        db.Terms.Add(term);
        db.ContentSections.Add(section);
        db.Lessons.Add(lesson);
        db.StudentBalances.Add(new StudentBalance { UserId = student.Id, CurrentBalance = 500m });
        db.StudentFacingAcademicScopes.Add(NonMatchingScope(StudentFacingScopeOwnerType.Package, package.Id));
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var handler = new PurchaseContentCommandHandler(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            new PromotionalBalanceService(db),
            new SalesTargetResolver(db),
            new DiscountEngine(db, academicScope),
            academicScope: academicScope);

        var result = await handler.Handle(new PurchaseContentCommand(student.Id, CodeType.Lesson, lesson.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", result.Errors ?? []);
        Assert.Equal(500m, db.StudentBalances.Single(x => x.UserId == student.Id).CurrentBalance);
        Assert.Empty(db.StudentAccessGrants);
    }

    private static async Task<User> SeedStudentAsync(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Student {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = user.Id,
            DateOfBirth = DateTime.UtcNow.AddYears(-16),
            Gender = Gender.Male,
            Governorate = "Cairo",
            Address = "Address",
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Package> SeedPackageAsync(AppDbContext db, string name, decimal price)
    {
        var subject = new Subject { Name = name, NormalizedName = $"{name.ToUpperInvariant().Replace(' ', '_')}_{Guid.NewGuid():N}", Description = "Subject" };
        var package = new Package
        {
            Name = name,
            Description = "Package",
            Price = price,
            IsActive = true,
            Subject = subject,
            TargetGrade = "FirstSecondary",
            TeacherId = Guid.NewGuid()
        };
        db.AddRange(subject, package);
        await db.SaveChangesAsync();
        return package;
    }

    private static StudentFacingAcademicScope NonMatchingScope(StudentFacingScopeOwnerType ownerType, Guid ownerId)
    {
        return new StudentFacingAcademicScope
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        };
    }
}
