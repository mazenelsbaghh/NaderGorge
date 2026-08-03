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
