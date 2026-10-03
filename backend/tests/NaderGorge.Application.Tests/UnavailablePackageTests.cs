using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class UnavailablePackageTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    public async Task UpdatingVisibility_PersistsOptInWithoutEnablingPurchase_AndPreservesOmittedSettings(bool? showWhenUnavailable, bool expectedVisibility)
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = await SeedUnavailableHierarchyAsync(db);
        var package = video.Lesson.ContentSection.Term.Package;
        var command = new UpdatePackageCommand(package.Id, package.Name, package.Description, package.Price, false)
        {
            ShowWhenUnavailable = showWhenUnavailable
        };

        var response = await new UpdatePackageCommandHandler(db).Handle(command, CancellationToken.None);
        db.ChangeTracker.Clear();
        var persisted = await db.Packages.FindAsync(package.Id);

        Assert.True(response.Success);
        Assert.Equal(expectedVisibility, persisted!.ShowWhenUnavailable);
        Assert.False(persisted.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VisibleUnavailablePackage_AppearsInStudentCatalogWithExistingAccessState(bool subscribed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Catalog student", "15955");
        db.StudentProfiles.Add(new StudentProfile { User = student, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary });
        var video = await SeedUnavailableHierarchyAsync(db);
        var packageId = video.Lesson.ContentSection.Term.PackageId;
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package, OwnerId = packageId,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        if (subscribed)
            db.StudentAccessGrants.Add(new StudentAccessGrant { UserId = student.Id, GrantType = CodeType.Package, PackageId = packageId, IsActive = true });
        await db.SaveChangesAsync();
        var handler = new GetPackagesQueryHandler(db, new AccessCheckService(db), new AcademicScopeService(db));

        var response = await handler.Handle(new GetPackagesQuery(student.Id), CancellationToken.None);

        Assert.True(response.Success);
        var package = Assert.Single(response.Data!);
        Assert.Equal(packageId, package.Id);
        Assert.False(package.IsActive);
        Assert.Equal(subscribed, package.HasDirectPackageAccess);
    }

    [Theory]
    [InlineData(CodeType.Package)]
    [InlineData(CodeType.Term)]
    [InlineData(CodeType.Month)]
    [InlineData(CodeType.Lesson)]
    public async Task VisibleUnavailablePackage_RejectsEveryPurchaseWithoutDebitingOrGranting(CodeType contentType)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Buyer", "15951");
        var video = await SeedUnavailableHierarchyAsync(db);
        var contentId = contentType switch
        {
            CodeType.Package => video.Lesson.ContentSection.Term.PackageId,
            CodeType.Term => video.Lesson.ContentSection.TermId,
            CodeType.Month => video.Lesson.ContentSectionId,
            CodeType.Lesson => video.LessonId,
            _ => video.Id
        };
        var handler = new PurchaseContentCommandHandler(db,
            new BalanceService(db, NullLogger<BalanceService>.Instance),
            new PromotionalBalanceService(db), new SalesTargetResolver(db), new DiscountEngine(db));

        var response = await handler.Handle(new PurchaseContentCommand(student.Id, contentType, contentId), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("هذا المحتوى غير متاح للشراء حالياً.", response.Message);
        Assert.Empty(db.StudentAccessGrants);
        Assert.Empty(db.StudentBalances);
        Assert.Empty(db.BalanceTransactions);
    }

    [Fact]
    public async Task VisibleUnavailablePackage_DoesNotMakeItsVideoEligibleForAcquisition()
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = await SeedUnavailableHierarchyAsync(db);

        var target = await new SalesTargetResolver(db).ResolveFromCodeTypeAsync(CodeType.Video, video.Id);

        Assert.NotNull(target);
        Assert.False(target.IsSaleEligible);
    }

    [Fact]
    public async Task VisibleUnavailablePackage_KeepsExistingSubscribersAccessWithoutOpeningContentToOthers()
    {
        await using var db = TestAppDbContextFactory.Create();
        var subscriber = await TestAppDbContextFactory.SeedUserAsync(db, "Subscriber", "15952");
        var visitor = await TestAppDbContextFactory.SeedUserAsync(db, "Visitor", "15953");
        var video = await SeedUnavailableHierarchyAsync(db);
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = subscriber.Id, GrantType = CodeType.Package,
            PackageId = video.Lesson.ContentSection.Term.PackageId, IsActive = true
        });
        await db.SaveChangesAsync();
        var access = new AccessCheckService(db);

        Assert.True(await access.HasAccessToPackageAsync(subscriber.Id, video.Lesson.ContentSection.Term.PackageId));
        Assert.True(await access.HasAccessToVideoAsync(subscriber.Id, video.Id));
        Assert.False(await access.HasAccessToVideoAsync(visitor.Id, video.Id));
    }

    private static async Task<LessonVideo> SeedUnavailableHierarchyAsync(AppDbContext db)
    {
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "15954");
        var teacher = new TeacherProfile { User = teacherUser, IsVisibleToStudents = true, IsContentVisibleToStudents = true };
        var subject = new Subject { Name = "History", NormalizedName = "HISTORY" };
        var package = new Package
        {
            Name = "Center package", Description = "Existing subscribers", Price = 200m,
            Subject = subject, Teacher = teacher, IsActive = false, ShowWhenUnavailable = true
        };
        var term = new Term { Package = package, Title = "Term", Price = 100m };
        var section = new ContentSection { Term = term, Title = "Month", Price = 50m };
        var lesson = new Lesson { ContentSection = section, Title = "Lesson", Summary = "History lesson", Price = 20m };
        var video = new LessonVideo
        {
            Lesson = lesson, Title = "Video", Provider = "youtube", ProviderVideoId = "test-video",
            VideoType = new VideoType { Name = "Explanation" }, IsActive = true
        };
        db.LessonVideos.Add(video);
        await db.SaveChangesAsync();
        return video;
    }
}
