using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Gifts.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Gifts;

public sealed class GiftHierarchyPostgresTests
{
    [Fact]
    public async Task ParentLookups_KeepDirectLessonInsideSelectedTeacherAndPackage()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var teacherUser = new User { FullName = "Gift teacher", PhoneNumber = "01000000041", PasswordHash = "test" };
        var otherTeacherUser = new User { FullName = "Other teacher", PhoneNumber = "01000000042", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = teacherUser };
        var otherTeacher = new TeacherProfile { User = otherTeacherUser };
        var subject = new Subject { Name = "Gift subject", NormalizedName = "gift subject" };
        var package = new Package
        {
            Name = "Selected package", Description = "Direct lessons", Price = 100m,
            Teacher = teacher, Subject = subject, TargetGrade = "SecondSecondary",
            ContentMode = PackageContentMode.LessonsOnly
        };
        var otherPackage = new Package
        {
            Name = "Unrelated package", Description = "Other teacher", Price = 100m,
            Teacher = otherTeacher, Subject = subject, TargetGrade = "SecondSecondary",
            ContentMode = PackageContentMode.LessonsOnly
        };
        var term = new Term { Title = "Internal term", Package = package, IsSystemContainer = true };
        var section = new ContentSection { Title = "Internal section", Term = term, IsSystemContainer = true };
        var lesson = new Lesson { Title = "Requested gift lesson", ContentSection = section };
        var otherTerm = new Term { Title = "Other term", Package = otherPackage, IsSystemContainer = true };
        var otherSection = new ContentSection { Title = "Other section", Term = otherTerm, IsSystemContainer = true };
        var otherLesson = new Lesson { Title = "Unrelated gift lesson", ContentSection = otherSection };
        db.AddRange(teacherUser, otherTeacherUser, teacher, otherTeacher, subject,
            package, otherPackage, term, section, lesson, otherTerm, otherSection, otherLesson);
        await db.SaveChangesAsync();

        var handler = new GetGiftTargetsLookupQueryHandler(db);
        var packages = await handler.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.Package, teacher.Id), default);
        var terms = await handler.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.Term, teacher.Id, ParentId: package.Id), default);
        var sections = await handler.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.ContentSection, teacher.Id, ParentId: term.Id), default);
        var lessons = await handler.Handle(new GetGiftTargetsLookupQuery(GiftTargetType.Lesson, teacher.Id, ParentId: section.Id), default);

        Assert.Equal(package.Id, Assert.Single(packages.Data!).Id);
        Assert.True(Assert.Single(terms.Data!).IsSystemContainer);
        Assert.Equal(term.Id, terms.Data![0].Id);
        Assert.True(Assert.Single(sections.Data!).IsSystemContainer);
        Assert.Equal(section.Id, sections.Data![0].Id);
        Assert.Equal(lesson.Id, Assert.Single(lessons.Data!).Id);

        var wrongTeacher = await handler.Handle(new GetGiftTargetsLookupQuery(
            GiftTargetType.Lesson, otherTeacher.Id, ParentId: section.Id), default);
        Assert.Empty(wrongTeacher.Data!);
    }
}
