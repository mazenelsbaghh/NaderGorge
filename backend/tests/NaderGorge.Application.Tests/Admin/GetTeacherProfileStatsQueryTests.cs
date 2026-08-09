using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Teachers.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Admin;

public sealed class GetTeacherProfileStatsQueryTests
{
    [Fact]
    public async Task ProductionRegression_20260809_GranularSalesQueriesReturnStats()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        var teacher = await SeedTeacherSalesAsync(db);

        var response = await new GetTeacherProfileStatsQueryHandler(db)
            .Handle(new GetTeacherProfileStatsQuery(teacher.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(4, response.Data!.StudentsCount);
        var packageSales = Assert.Single(response.Data.PackageSales);
        Assert.Equal((1, 1, 1, 1),
            (packageSales.PackageBuyers, packageSales.TermBuyers, packageSales.SectionBuyers, packageSales.LessonBuyers));
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static async Task<TeacherProfile> SeedTeacherSalesAsync(AppDbContext db)
    {
        var teacherUser = NewUser("teacher");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "English", NormalizedName = "ENGLISH" };
        var package = new Package { Id = Guid.NewGuid(), Name = "Grade 12", Subject = subject, Teacher = teacher };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term 1", Package = package };
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Month 1", Term = term };
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson 1", ContentSection = section };
        var students = Enumerable.Range(1, 4).Select(index => NewUser($"student-{index}")).ToArray();

        db.AddRange(teacher, subject, package, term, section, lesson);
        db.StudentAccessGrants.AddRange(
            NewGrant(students[0], CodeType.Package, packageId: package.Id),
            NewGrant(students[1], CodeType.Term, termId: term.Id),
            NewGrant(students[2], CodeType.Month, sectionId: section.Id),
            NewGrant(students[3], CodeType.Lesson, lessonId: lesson.Id));
        await db.SaveChangesAsync();
        return teacher;
    }

    private static User NewUser(string phoneNumber) => new()
    {
        Id = Guid.NewGuid(),
        FullName = phoneNumber,
        PhoneNumber = phoneNumber,
        PasswordHash = "test-hash"
    };

    private static StudentAccessGrant NewGrant(
        User student,
        CodeType grantType,
        Guid? packageId = null,
        Guid? termId = null,
        Guid? sectionId = null,
        Guid? lessonId = null) => new()
    {
        Id = Guid.NewGuid(),
        User = student,
        GrantType = grantType,
        PackageId = packageId,
        TermId = termId,
        ContentSectionId = sectionId,
        LessonId = lessonId
    };
}
