using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class FullPackagePurchasePolicyTests
{
    [Fact]
    public async Task DirectPurchase_RejectsDisabledFullPackageBeforeAnyWalletOrGrantMutation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Full package buyer", "15901");
        var seeded = await TestAppDbContextFactory.SeedPackageAsync(db, "Disabled full package", price: 100m);
        var package = await db.Packages.FindAsync(seeded.PackageId);
        package!.ContentMode = PackageContentMode.TermWithSections;
        package.AllowFullPackagePurchase = false;
        await db.SaveChangesAsync();

        var handler = new PurchaseContentCommandHandler(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            new PromotionalBalanceService(db),
            new SalesTargetResolver(db),
            new DiscountEngine(db));

        var result = await handler.Handle(
            new PurchaseContentCommand(student.Id, CodeType.Package, package.Id),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(FullPackagePurchasePolicy.ErrorCode, result.Errors ?? []);
        Assert.Equal(FullPackagePurchasePolicy.ErrorMessage, result.Message);
        Assert.Empty(db.StudentAccessGrants);
        Assert.Empty(db.StudentBalances);
        Assert.Empty(db.BalanceTransactions);
    }

    [Fact]
    public async Task SharedPackagePurchase_RejectsSelectedDisabledFullPackageBeforeDebit()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Shared package buyer", "15902");
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = student.Id,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });

        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Shared package teacher", "15903");
        var teacher = new TeacherProfile
        {
            UserId = teacherUser.Id,
            User = teacherUser,
            Bio = "Teacher",
            ContactInfo = "Contact",
            Specialization = "FirstSecondary",
            IsContentVisibleToStudents = true
        };
        var subject = new Subject
        {
            Name = "Shared subject",
            NormalizedName = $"SHARED_{Guid.NewGuid():N}",
            Description = "Subject"
        };
        var disabledPackage = new Package
        {
            Name = "Disabled full package",
            Description = "Full-year package",
            Price = 100m,
            Subject = subject,
            Teacher = teacher,
            TargetGrade = "FirstSecondary",
            ContentMode = PackageContentMode.TermWithSections,
            AllowFullPackagePurchase = false
        };
        var sharedPackage = new SharedTeacherPackage
        {
            Name = "Shared offer",
            Slug = $"shared-{Guid.NewGuid():N}",
            Description = "Shared offer",
            Price = 100m,
            IsPublished = true,
            CreatedByUserId = teacherUser.Id
        };
        sharedPackage.Teachers.Add(new SharedTeacherPackageTeacher
        {
            Teacher = teacher,
            Subject = subject,
            AllocationMode = TeacherAllocationMode.Percentage,
            AllocationValue = 100m
        });
        sharedPackage.Items.Add(new SharedTeacherPackageItem
        {
            Teacher = teacher,
            Subject = subject,
            ContentType = SalesTargetType.Package,
            ContentId = disabledPackage.Id,
            Price = 100m,
            IsIncluded = true
        });
        db.Packages.Add(disabledPackage);
        db.SharedTeacherPackages.Add(sharedPackage);
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.SharedTeacherPackage,
            OwnerId = sharedPackage.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        await db.SaveChangesAsync();

        var controller = CreateStudentSharedPackageController(db, student.Id);

        var action = await controller.Purchase(
            sharedPackage.Id,
            new PurchaseSharedPackageDto([], ConfirmLoss: false),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action);
        var payload = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains(FullPackagePurchasePolicy.ErrorCode, payload, StringComparison.Ordinal);
        Assert.Empty(db.StudentBalances);
        Assert.Empty(db.BalanceTransactions);
        Assert.Empty(db.StudentAccessGrants);
        Assert.Empty(db.TeacherFinancialEvents);
    }

    [Fact]
    public async Task SharedPackagePurchase_UnselectedDisabledAlternativeDoesNotBlockSelectedEnabledAlternative()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Alternative package buyer", "15904");
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = student.Id,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });
        db.StudentBalances.Add(new StudentBalance { UserId = student.Id, CurrentBalance = 100m });

        var enabledTeacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Enabled alternative teacher", "15905");
        var disabledTeacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Disabled alternative teacher", "15906");
        var enabledTeacher = NewVisibleTeacher(enabledTeacherUser);
        var disabledTeacher = NewVisibleTeacher(disabledTeacherUser);
        var subject = new Subject
        {
            Name = "Alternative subject",
            NormalizedName = $"ALTERNATIVE_{Guid.NewGuid():N}",
            Description = "Subject"
        };
        var enabledPackage = NewFullPackage("Enabled package", subject, enabledTeacher);
        var disabledPackage = NewFullPackage("Disabled alternative", subject, disabledTeacher);
        disabledPackage.AllowFullPackagePurchase = false;
        var sharedPackage = new SharedTeacherPackage
        {
            Name = "Alternative offer",
            Slug = $"alternative-{Guid.NewGuid():N}",
            Description = "Choose one teacher",
            Price = 100m,
            IsPublished = true,
            CreatedByUserId = enabledTeacherUser.Id
        };
        sharedPackage.Teachers.Add(NewSharedTeacher(sharedPackage, enabledTeacher, subject));
        sharedPackage.Teachers.Add(NewSharedTeacher(sharedPackage, disabledTeacher, subject));
        var enabledItem = NewSharedPackageItem(sharedPackage, enabledTeacher, subject, enabledPackage);
        var disabledItem = NewSharedPackageItem(sharedPackage, disabledTeacher, subject, disabledPackage);
        sharedPackage.Items.Add(enabledItem);
        sharedPackage.Items.Add(disabledItem);

        db.Packages.AddRange(enabledPackage, disabledPackage);
        db.SharedTeacherPackages.Add(sharedPackage);
        db.StudentFacingAcademicScopes.AddRange(
            PlatformScope(StudentFacingScopeOwnerType.SharedTeacherPackage, sharedPackage.Id),
            PlatformScope(StudentFacingScopeOwnerType.SharedTeacherPackageItem, enabledItem.Id),
            PlatformScope(StudentFacingScopeOwnerType.Package, enabledPackage.Id));
        await db.SaveChangesAsync();

        var controller = CreateStudentSharedPackageController(db, student.Id);
        var action = await controller.Purchase(
            sharedPackage.Id,
            new PurchaseSharedPackageDto(
                [new SharedPackageTeacherSelectionDto(subject.Id, enabledTeacher.Id)],
                ConfirmLoss: false),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(action);
        Assert.Equal(0m, db.StudentBalances.Single(balance => balance.UserId == student.Id).CurrentBalance);
        var grant = Assert.Single(db.StudentAccessGrants);
        Assert.Equal(enabledPackage.Id, grant.PackageId);
        Assert.Single(db.TeacherFinancialEvents);
    }

    private static StudentSharedPackagesController CreateStudentSharedPackageController(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        Guid studentId) =>
        new(
            db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            new TeacherAccountingService(db),
            new AcademicScopeService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, studentId.ToString())],
                        "test"))
                }
            }
        };

    private static TeacherProfile NewVisibleTeacher(User user) => new()
    {
        UserId = user.Id,
        User = user,
        Bio = "Teacher",
        ContactInfo = "Contact",
        Specialization = "FirstSecondary",
        IsContentVisibleToStudents = true
    };

    private static Package NewFullPackage(
        string name,
        Subject subject,
        TeacherProfile teacher) => new()
    {
        Name = name,
        Description = name,
        Price = 100m,
        Subject = subject,
        Teacher = teacher,
        TargetGrade = "FirstSecondary",
        ContentMode = PackageContentMode.TermWithSections,
        AllowFullPackagePurchase = true
    };

    private static SharedTeacherPackageTeacher NewSharedTeacher(
        SharedTeacherPackage sharedPackage,
        TeacherProfile teacher,
        Subject subject) => new()
    {
        SharedTeacherPackage = sharedPackage,
        Teacher = teacher,
        Subject = subject,
        AllocationMode = TeacherAllocationMode.Percentage,
        AllocationValue = 100m
    };

    private static SharedTeacherPackageItem NewSharedPackageItem(
        SharedTeacherPackage sharedPackage,
        TeacherProfile teacher,
        Subject subject,
        Package package) => new()
    {
        SharedTeacherPackage = sharedPackage,
        Teacher = teacher,
        Subject = subject,
        ContentType = SalesTargetType.Package,
        ContentId = package.Id,
        Price = 100m,
        IsIncluded = true
    };

    private static StudentFacingAcademicScope PlatformScope(
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId) => new()
    {
        OwnerType = ownerType,
        OwnerId = ownerId,
        ScopeLevel = AcademicScopeLevel.PlatformWide
    };
}
