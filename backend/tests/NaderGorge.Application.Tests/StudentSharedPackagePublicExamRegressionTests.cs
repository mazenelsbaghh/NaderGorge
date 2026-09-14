using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class StudentSharedPackagePublicExamRegressionTests
{
    [Theory]
    [InlineData(UnavailablePublicExamCase.InactiveExam)]
    [InlineData(UnavailablePublicExamCase.UnpublishedProduct)]
    [InlineData(UnavailablePublicExamCase.DisabledProduct)]
    [InlineData(UnavailablePublicExamCase.NotYetAvailableProduct)]
    [InlineData(UnavailablePublicExamCase.ExpiredProduct)]
    public async Task ProductionRegression_UnavailablePublicExam_IsHiddenAndRejectedBeforePurchasePersistence(
        UnavailablePublicExamCase unavailableCase)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedSharedPublicExamPackageAsync(db, unavailableCase);
        var controller = CreateController(db, fixture.StudentId);

        var detailAction = await controller.Detail(fixture.SharedPackageId, CancellationToken.None);

        var detail = Assert.IsType<OkObjectResult>(detailAction);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(detail.Value));
        var items = document.RootElement.GetProperty("data").GetProperty("items");
        Assert.Empty(items.EnumerateArray());

        var purchaseAction = await controller.Purchase(
            fixture.SharedPackageId,
            new PurchaseSharedPackageDto([], ConfirmLoss: false),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(purchaseAction);
        Assert.Equal(100m, db.StudentBalances.Single(balance => balance.UserId == fixture.StudentId).CurrentBalance);
        Assert.Empty(db.BalanceTransactions);
        Assert.Empty(db.StudentAccessGrants);
        Assert.Empty(db.TeacherFinancialEvents);
    }

    [Fact]
    public async Task ProductionRegression_AvailablePublicExamPurchase_GrantKeepsProductAndExamIdentity()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedSharedPublicExamPackageAsync(db);
        var controller = CreateController(db, fixture.StudentId);

        var action = await controller.Purchase(
            fixture.SharedPackageId,
            new PurchaseSharedPackageDto([], ConfirmLoss: false),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(action);
        var grant = Assert.Single(db.StudentAccessGrants);
        Assert.Equal(CodeType.Exam, grant.GrantType);
        Assert.Equal(fixture.PublicExamProductId, grant.PublicExamProductId);
        Assert.Equal(fixture.ExamId, grant.ExamId);
        Assert.Equal(0m, db.StudentBalances.Single(balance => balance.UserId == fixture.StudentId).CurrentBalance);
        Assert.Single(db.BalanceTransactions);
        Assert.Single(db.TeacherFinancialEvents);
    }

    private static async Task<SharedPublicExamFixture> SeedSharedPublicExamPackageAsync(
        AppDbContext db,
        UnavailablePublicExamCase? unavailableCase = null)
    {
        var student = await TestAppDbContextFactory.SeedUserAsync(
            db,
            "Shared public exam student",
            Guid.NewGuid().ToString("N")[..11]);
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = student.Id,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });
        db.StudentBalances.Add(new StudentBalance
        {
            UserId = student.Id,
            CurrentBalance = 100m
        });

        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(
            db,
            "Shared public exam teacher",
            Guid.NewGuid().ToString("N")[..11]);
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
            Name = "Public exam subject",
            NormalizedName = $"PUBLIC_EXAM_{Guid.NewGuid():N}",
            Description = "Subject"
        };
        var exam = new Exam
        {
            Title = "Shared public exam",
            Description = "Regression exam",
            PassingScore = 50m,
            TotalScore = 100m,
            IsActive = true,
            CreatedByTeacherId = teacher.Id,
            CreatedByTeacher = teacher
        };
        var now = DateTime.UtcNow;
        var product = new PublicExamProduct
        {
            ExamId = exam.Id,
            Exam = exam,
            Slug = $"shared-public-exam-{Guid.NewGuid():N}",
            IsPublished = true,
            IsPaid = true,
            Price = 100m,
            TeacherId = teacher.Id,
            Teacher = teacher,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevel = nameof(GradeLevel.FirstSecondary),
            AvailableFrom = now.AddMinutes(-5),
            AvailableUntil = now.AddMinutes(5),
            CreatedByUserId = teacherUser.Id,
            CreatedByUser = teacherUser
        };

        ApplyUnavailableState(unavailableCase, exam, product, now);

        var sharedPackage = new SharedTeacherPackage
        {
            Name = "Shared public exam offer",
            Slug = $"shared-public-exam-offer-{Guid.NewGuid():N}",
            Description = "Public exam offer",
            Price = 100m,
            IsPublished = true,
            CreatedByUserId = teacherUser.Id,
            CreatedByUser = teacherUser
        };
        sharedPackage.Teachers.Add(new SharedTeacherPackageTeacher
        {
            Teacher = teacher,
            Subject = subject,
            AllocationMode = TeacherAllocationMode.Percentage,
            AllocationValue = 100m
        });
        var item = new SharedTeacherPackageItem
        {
            Teacher = teacher,
            Subject = subject,
            ContentType = SalesTargetType.PublicExam,
            ContentId = product.Id,
            Price = 100m,
            IsIncluded = true
        };
        sharedPackage.Items.Add(item);

        db.PublicExamProducts.Add(product);
        db.SharedTeacherPackages.Add(sharedPackage);
        db.StudentFacingAcademicScopes.AddRange(
            PlatformScope(StudentFacingScopeOwnerType.SharedTeacherPackage, sharedPackage.Id),
            PlatformScope(StudentFacingScopeOwnerType.SharedTeacherPackageItem, item.Id),
            PlatformScope(StudentFacingScopeOwnerType.PublicExamProduct, product.Id));
        await db.SaveChangesAsync();

        return new SharedPublicExamFixture(student.Id, sharedPackage.Id, product.Id, exam.Id);
    }

    private static void ApplyUnavailableState(
        UnavailablePublicExamCase? unavailableCase,
        Exam exam,
        PublicExamProduct product,
        DateTime now)
    {
        switch (unavailableCase)
        {
            case UnavailablePublicExamCase.InactiveExam:
                exam.IsActive = false;
                break;
            case UnavailablePublicExamCase.UnpublishedProduct:
                product.IsPublished = false;
                break;
            case UnavailablePublicExamCase.DisabledProduct:
                product.DisabledAt = now.AddMinutes(-1);
                break;
            case UnavailablePublicExamCase.NotYetAvailableProduct:
                product.AvailableFrom = now.AddMinutes(5);
                break;
            case UnavailablePublicExamCase.ExpiredProduct:
                product.AvailableUntil = now.AddMinutes(-1);
                break;
        }
    }

    private static StudentSharedPackagesController CreateController(AppDbContext db, Guid studentId) =>
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

    private static StudentFacingAcademicScope PlatformScope(
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId) => new()
    {
        OwnerType = ownerType,
        OwnerId = ownerId,
        ScopeLevel = AcademicScopeLevel.PlatformWide
    };

    public enum UnavailablePublicExamCase
    {
        InactiveExam,
        UnpublishedProduct,
        DisabledProduct,
        NotYetAvailableProduct,
        ExpiredProduct
    }

    private sealed record SharedPublicExamFixture(
        Guid StudentId,
        Guid SharedPackageId,
        Guid PublicExamProductId,
        Guid ExamId);
}
