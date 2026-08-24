using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class FlexiblePackageContentTests
{
    [Fact]
    public async Task CreatePackage_LessonsOnlyCreatesHiddenRootContainers()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db);
        var handler = new CreatePackageCommandHandler(db, new TeacherAuthorizationService(db));

        var packageCreation = await handler.Handle(CreatePackage(subject, teacher, PackageContentMode.LessonsOnly), CancellationToken.None);

        Assert.True(packageCreation.Success);
        var package = await db.Packages.SingleAsync(item => item.Id == packageCreation.Data);
        var rootTerm = await db.Terms.SingleAsync(item => item.PackageId == package.Id && item.IsSystemContainer);
        var rootSection = await db.ContentSections.SingleAsync(item => item.TermId == rootTerm.Id && item.IsSystemContainer);

        Assert.Equal(PackageContentMode.LessonsOnly, package.ContentMode);
        Assert.Equal("الحصص المباشرة", rootSection.Title);
    }

    [Fact]
    public async Task SectionWithLessons_AllowsDirectSectionAndLessonCreation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db);
        var packageHandler = new CreatePackageCommandHandler(db, new TeacherAuthorizationService(db));
        var packageCreation = await packageHandler.Handle(CreatePackage(subject, teacher, PackageContentMode.SectionWithLessons), CancellationToken.None);
        Assert.True(packageCreation.Success);

        var rootTerm = await db.Terms.SingleAsync(item => item.PackageId == packageCreation.Data && item.IsSystemContainer);
        var sectionHandler = new CreateSectionCommandHandler(db, new TeacherAuthorizationService(db));
        var sectionCreation = await sectionHandler.Handle(new CreateSectionCommand("الوحدة الأولى", 1, rootTerm.Id, 0), CancellationToken.None);

        Assert.True(sectionCreation.Success);
        var lessonHandler = new CreateLessonCommandHandler(db, new TeacherAuthorizationService(db));
        var lessonCreation = await lessonHandler.Handle(new CreateLessonCommand("الحصة الأولى", "", 1, sectionCreation.Data, null, 0), CancellationToken.None);

        Assert.True(lessonCreation.Success);
        Assert.True(await db.Lessons.AnyAsync(item => item.Id == lessonCreation.Data && item.ContentSectionId == sectionCreation.Data));
    }

    [Fact]
    public async Task DirectContent_UsesPackagePriceWhenStoredContainerPriceIsStale()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db);
        var packageHandler = new CreatePackageCommandHandler(db, new TeacherAuthorizationService(db));
        var packageCreation = await packageHandler.Handle(
            CreatePackage(subject, teacher, PackageContentMode.SectionWithLessons),
            CancellationToken.None);
        var rootTerm = await db.Terms.SingleAsync(term => term.PackageId == packageCreation.Data && term.IsSystemContainer);
        rootTerm.Price = 0;
        await db.SaveChangesAsync();

        var target = await new SalesTargetResolver(db).ResolveFromCodeTypeAsync(CodeType.Term, rootTerm.Id);

        Assert.NotNull(target);
        Assert.Equal(100, target.Price);
    }

    [Fact]
    public async Task UpdatePackage_SynchronizesDirectContentPrice()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db);
        var packageHandler = new CreatePackageCommandHandler(db, new TeacherAuthorizationService(db));
        var packageCreation = await packageHandler.Handle(
            CreatePackage(subject, teacher, PackageContentMode.SectionWithLessons),
            CancellationToken.None);
        var package = await db.Packages.SingleAsync(item => item.Id == packageCreation.Data);
        var updateHandler = new UpdatePackageCommandHandler(db);

        var update = await updateHandler.Handle(
            new UpdatePackageCommand(package.Id, package.Name, package.Description, 160, package.IsActive),
            CancellationToken.None);

        var rootTermPrice = await db.Terms
            .Where(term => term.PackageId == package.Id && term.IsSystemContainer)
            .Select(term => term.Price)
            .SingleAsync();
        Assert.True(update.Success);
        Assert.Equal(160, rootTermPrice);
    }

    [Fact]
    public async Task ProductionRegression_StandaloneLesson_CreatesReadyLessonWithoutVisiblePackageLevels()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db);
        var packageHandler = new CreatePackageCommandHandler(db, new TeacherAuthorizationService(db));

        var packageCreation = await packageHandler.Handle(
            CreatePackage(subject, teacher, PackageContentMode.SingleLesson),
            CancellationToken.None);

        Assert.True(packageCreation.Success);
        var package = await db.Packages.SingleAsync(item => item.Id == packageCreation.Data);
        var rootTerm = await db.Terms.SingleAsync(item => item.PackageId == package.Id && item.IsSystemContainer);
        var rootSection = await db.ContentSections.SingleAsync(item => item.TermId == rootTerm.Id && item.IsSystemContainer);
        var lesson = await db.Lessons.SingleAsync(item => item.ContentSectionId == rootSection.Id);

        Assert.Equal(PackageContentMode.SingleLesson, package.ContentMode);
        Assert.Equal(package.Name, lesson.Title);
        Assert.Equal(package.Description, lesson.Summary);
        Assert.Equal(package.Price, lesson.Price);

        var lessonHandler = new CreateLessonCommandHandler(db, new TeacherAuthorizationService(db));
        var extraLessonCreation = await lessonHandler.Handle(
            new CreateLessonCommand("حصة ثانية", "", 2, rootSection.Id, null, 0),
            CancellationToken.None);

        Assert.False(extraLessonCreation.Success);
        Assert.Single(await db.Lessons.Where(item => item.ContentSectionId == rootSection.Id).ToListAsync());
    }

    private static CreatePackageCommand CreatePackage(Subject subject, TeacherProfile teacher, PackageContentMode mode)
    {
        return new CreatePackageCommand(
            "كورس مرن",
            "اختبار هيكل الكورس",
            100,
            subject.Id,
            "FirstSecondary",
            teacher.Id,
            null,
            [new AcademicScopeDto(AcademicScopeLevel.Exact, EducationStage.Secondary, GradeLevel.FirstSecondary, subject.Id)])
        {
            ContentMode = mode
        };
    }

    private static async Task<(TeacherProfile Teacher, Subject Subject)> SeedTeacherAndSubjectAsync(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Flexible Teacher", $"9{Guid.NewGuid():N}"[..11]);
        var subject = new Subject
        {
            Name = "Flexible Subject",
            NormalizedName = Guid.NewGuid().ToString("N"),
            Description = "Subject"
        };
        var teacher = new TeacherProfile
        {
            UserId = user.Id,
            Specialization = "FirstSecondary",
            Bio = "Bio",
            ContactInfo = "Contact"
        };

        db.Subjects.Add(subject);
        db.TeacherProfiles.Add(teacher);
        db.TeacherSubjects.Add(new TeacherSubject { Teacher = teacher, Subject = subject });
        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
        {
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            Subject = subject
        });
        await db.SaveChangesAsync();
        return (teacher, subject);
    }
}
