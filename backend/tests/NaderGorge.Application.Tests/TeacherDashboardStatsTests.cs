using NaderGorge.Application.Features.Teacher;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class TeacherDashboardStatsTests
{
    [Fact]
    public async Task Dashboard_stats_share_purchase_gift_and_active_student_rules_with_content_summary()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01087000001");
        var mixedStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Mixed", "01087000002");
        var giftOnlyStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Gift", "01087000003");
        var expiredStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Expired", "01087000004");
        var cancelledStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Cancelled", "01087000005");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math", NormalizedName = "MATH" };
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Grade 10",
            SubjectId = subject.Id,
            Subject = subject,
            TeacherId = teacher.Id,
            Teacher = teacher
        };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term", PackageId = package.Id, Package = package };
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Section", TermId = term.Id, Term = term };
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson", ContentSectionId = section.Id, ContentSection = section };
        var now = DateTime.UtcNow;

        var giftPackage = Grant(mixedStudent.Id, CodeType.Package, package.Id);
        giftPackage.GiftRecipientId = Guid.NewGuid();
        var giftLesson = Grant(giftOnlyStudent.Id, CodeType.Lesson, lesson.Id);
        giftLesson.GiftRecipientId = Guid.NewGuid();
        var expiredSection = Grant(expiredStudent.Id, CodeType.Month, section.Id);
        expiredSection.ExpiresAt = now.AddMinutes(-1);
        var cancelledPackage = Grant(cancelledStudent.Id, CodeType.Package, package.Id);
        cancelledPackage.IsActive = false;
        cancelledPackage.CancelledAt = now;

        db.AddRange(teacher, subject, package, term, section, lesson);
        db.StudentAccessGrants.AddRange(
            giftPackage,
            Grant(mixedStudent.Id, CodeType.Term, term.Id),
            giftLesson,
            expiredSection,
            cancelledPackage);
        await db.SaveChangesAsync();

        var response = await new GetTeacherDashboardStatsQueryHandler(db)
            .Handle(new GetTeacherDashboardStatsQuery(teacherUser.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.ActiveStudentsCount);
        var packageSales = Assert.Single(response.Data.PackageSales);
        Assert.Equal((1, 1, 1, 1),
            (packageSales.PackageBuyers, packageSales.TermBuyers, packageSales.SectionBuyers, packageSales.LessonBuyers));
        Assert.Equal((2, 1), (packageSales.PurchasedStudents, packageSales.GiftStudents));
    }

    private static StudentAccessGrant Grant(Guid userId, CodeType type, Guid targetId)
    {
        var grant = new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GrantType = type,
            GrantedAt = DateTime.UtcNow,
            IsActive = true
        };

        switch (type)
        {
            case CodeType.Package: grant.PackageId = targetId; break;
            case CodeType.Term: grant.TermId = targetId; break;
            case CodeType.Month: grant.ContentSectionId = targetId; break;
            case CodeType.Lesson: grant.LessonId = targetId; break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported test grant type.");
        }

        return grant;
    }
}
