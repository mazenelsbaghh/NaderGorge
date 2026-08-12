using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Teacher;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class TeacherStudentsQueryTests
{
    [Fact]
    public async Task StudentsList_UsesDistinctEffectiveFactsAcrossAllContentLevels()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var teacherUser = User("Teacher", "01093000001");
        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Math", NormalizedName = "MATH" };
        var firstPackage = new Package { Name = "First package", Subject = subject, Teacher = teacher };
        var firstTerm = new Term { Title = "First term", Package = firstPackage };
        var firstSection = new ContentSection { Title = "First section", Term = firstTerm };
        var firstLesson = new Lesson { Title = "First lesson", ContentSection = firstSection };
        var secondPackage = new Package { Name = "Second package", Subject = subject, Teacher = teacher };
        var secondTerm = new Term { Title = "Second term", Package = secondPackage };
        var secondSection = new ContentSection { Title = "Second section", Term = secondTerm };
        var secondLesson = new Lesson { Title = "Second lesson", ContentSection = secondSection };
        var multiGrantStudent = User("Multi grant", "01093000002");
        var lessonOnlyStudent = User("Lesson only", "01093000003");
        var excludedStudent = User("Excluded", "01093000004");
        var profile = new StudentProfile
        {
            User = multiGrantStudent,
            StudentCode = "ST-100",
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondaryGrade3
        };
        var now = DateTime.UtcNow;
        var cancelledGrant = Grant(excludedStudent, CodeType.Package, firstPackage.Id, now.AddDays(-3));
        cancelledGrant.CancelledAt = now.AddDays(-2);
        var expiredGrant = Grant(excludedStudent, CodeType.Term, firstTerm.Id, now.AddDays(-3));
        expiredGrant.ExpiresAt = now.AddDays(-1);
        var inactiveGrant = Grant(excludedStudent, CodeType.Lesson, firstLesson.Id, now.AddDays(-3));
        inactiveGrant.IsActive = false;

        db.AddRange(
            teacher,
            subject,
            firstPackage,
            firstTerm,
            firstSection,
            firstLesson,
            secondPackage,
            secondTerm,
            secondSection,
            secondLesson,
            profile,
            lessonOnlyStudent,
            excludedStudent);
        db.StudentAccessGrants.AddRange(
            Grant(multiGrantStudent, CodeType.Package, firstPackage.Id, now.AddDays(-4)),
            Grant(multiGrantStudent, CodeType.Term, firstTerm.Id, now.AddDays(-3)),
            Grant(multiGrantStudent, CodeType.Month, firstSection.Id, now.AddDays(-2)),
            Grant(multiGrantStudent, CodeType.Lesson, secondLesson.Id, now.AddDays(-1)),
            Grant(lessonOnlyStudent, CodeType.Lesson, firstLesson.Id, now),
            cancelledGrant,
            expiredGrant,
            inactiveGrant);
        await db.SaveChangesAsync();

        var response = await new GetTeacherStudentsQueryHandler(db)
            .Handle(new GetTeacherStudentsQuery(teacherUser.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.Count);
        var multiGrantRow = Assert.Single(response.Data, student => student.Id == multiGrantStudent.Id);
        Assert.Equal((4, 2), (multiGrantRow.ActiveGrantCount, multiGrantRow.ActivePackageCount));
        Assert.Equal((now.AddDays(-4), now.AddDays(-1)),
            (multiGrantRow.ActivatedAt, multiGrantRow.LastActivationAt));
        Assert.Equal(secondPackage.Name, multiGrantRow.ActivatedPackageName);
        Assert.Equal("ST-100", multiGrantRow.StudentCode);
        var lessonOnlyRow = Assert.Single(response.Data, student => student.Id == lessonOnlyStudent.Id);
        Assert.Equal((1, 1), (lessonOnlyRow.ActiveGrantCount, lessonOnlyRow.ActivePackageCount));
        Assert.DoesNotContain(response.Data, student => student.Id == excludedStudent.Id);
    }

    private static User User(string fullName, string phoneNumber) => new()
    {
        FullName = fullName,
        PhoneNumber = phoneNumber,
        PasswordHash = "test-hash"
    };

    private static StudentAccessGrant Grant(
        User student,
        CodeType grantType,
        Guid targetId,
        DateTime grantedAt)
    {
        var grant = new StudentAccessGrant
        {
            User = student,
            GrantType = grantType,
            GrantedAt = grantedAt,
            IsActive = true
        };

        switch (grantType)
        {
            case CodeType.Package: grant.PackageId = targetId; break;
            case CodeType.Term: grant.TermId = targetId; break;
            case CodeType.Month: grant.ContentSectionId = targetId; break;
            case CodeType.Lesson: grant.LessonId = targetId; break;
            default: throw new ArgumentOutOfRangeException(nameof(grantType), grantType, "Unsupported test grant type.");
        }

        return grant;
    }
}
