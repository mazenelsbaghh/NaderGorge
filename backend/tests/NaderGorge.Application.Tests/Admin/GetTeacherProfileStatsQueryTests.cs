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

    [Fact]
    public async Task Profile_stats_use_purchase_wins_and_count_active_students_across_all_content_levels()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = NewUser("teacher-active-levels");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Science", NormalizedName = "SCIENCE" };
        var package = new Package { Id = Guid.NewGuid(), Name = "Grade 11", Subject = subject, Teacher = teacher };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term", Package = package };
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Section", Term = term };
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson", ContentSection = section };
        var mixedStudent = NewUser("student-mixed");
        var giftOnlyStudent = NewUser("student-gift");
        var expiredStudent = NewUser("student-expired");
        var inactiveStudent = NewUser("student-inactive");
        var cancelledStudent = NewUser("student-cancelled");
        var now = DateTime.UtcNow;

        var mixedGift = NewGrant(mixedStudent, CodeType.Package, packageId: package.Id);
        mixedGift.GiftRecipientId = Guid.NewGuid();
        var giftLesson = NewGrant(giftOnlyStudent, CodeType.Lesson, lessonId: lesson.Id);
        giftLesson.GiftRecipientId = Guid.NewGuid();
        giftLesson.ExpiresAt = now.AddDays(1);
        var expiredSection = NewGrant(expiredStudent, CodeType.Month, sectionId: section.Id);
        expiredSection.ExpiresAt = now.AddDays(-1);
        var inactiveLesson = NewGrant(inactiveStudent, CodeType.Lesson, lessonId: lesson.Id);
        inactiveLesson.IsActive = false;
        var cancelledPackage = NewGrant(cancelledStudent, CodeType.Package, packageId: package.Id);
        cancelledPackage.IsActive = false;
        cancelledPackage.CancelledAt = now;

        db.AddRange(teacher, subject, package, term, section, lesson);
        db.StudentAccessGrants.AddRange(
            mixedGift,
            NewGrant(mixedStudent, CodeType.Term, termId: term.Id),
            giftLesson,
            expiredSection,
            inactiveLesson,
            cancelledPackage);
        await db.SaveChangesAsync();

        var response = await new GetTeacherProfileStatsQueryHandler(db)
            .Handle(new GetTeacherProfileStatsQuery(teacher.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(4, response.Data!.StudentsCount);
        Assert.Equal(2, response.Data.ActiveStudentsCount);
        var packageSales = Assert.Single(response.Data.PackageSales);
        Assert.Equal((1, 1, 1, 2),
            (packageSales.PackageBuyers, packageSales.TermBuyers, packageSales.SectionBuyers, packageSales.LessonBuyers));
        Assert.Equal((3, 1), (packageSales.PurchasedStudents, packageSales.GiftStudents));
    }

    [Fact]
    public async Task Pending_essays_include_unassigned_owned_questions_and_exclude_graded_or_other_teachers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        var teacher = await SeedTeacherSalesAsync(db);
        var other = new TeacherProfile { User = NewUser("other-teacher") };
        var student = await db.Users.FirstAsync(x => x.Id != teacher.UserId);
        var subject = await db.Subjects.SingleAsync();
        var exam = new Exam { Title = "Essay exam", CreatedByTeacherId = teacher.Id };
        var attempt = new StudentExamAttempt { UserId = student.Id, Exam = exam };
        db.AddRange(other, exam, attempt);
        foreach (var (owner, grader, status) in new[]
        {
            (teacher.Id, (Guid?)null, EssaySubmissionStatus.WaitTeacher),
            (teacher.Id, (Guid?)null, EssaySubmissionStatus.WaitAI),
            (other.Id, (Guid?)teacher.Id, EssaySubmissionStatus.AIScored),
            (teacher.Id, (Guid?)teacher.Id, EssaySubmissionStatus.TeacherGraded),
            (other.Id, (Guid?)null, EssaySubmissionStatus.WaitTeacher)
        })
            db.EssaySubmissions.Add(new EssaySubmission { StudentId = student.Id, Attempt = attempt,
                Question = new EssayQuestion { Text = "Explain", SubjectId = subject.Id, CreatedByTeacherId = owner },
                GradedByTeacherId = grader, Status = status });
        await db.SaveChangesAsync();

        var result = await new GetTeacherProfileStatsQueryHandler(db).Handle(new(teacher.Id), default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, result.Data!.EssaysPendingCount);
        Assert.Equal(1, result.Data.EssaysGradedCount);
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
