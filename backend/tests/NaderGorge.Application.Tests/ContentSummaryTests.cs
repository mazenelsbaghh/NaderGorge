using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class ContentSummaryTests
{
    [Fact]
    public async Task Summary_separates_unique_buyers_and_gifts_and_builds_paid_combinations()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01081000001");
        var firstStudent = await TestAppDbContextFactory.SeedUserAsync(db, "First", "01081000002");
        var secondStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Second", "01081000003");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var firstPackage = PackageFor(teacher, "فيزياء");
        var secondPackage = PackageFor(teacher, "كيمياء");
        var now = DateTime.UtcNow;

        db.TeacherProfiles.Add(teacher);
        db.Packages.AddRange(firstPackage, secondPackage);
        db.StudentAccessGrants.AddRange(
            Grant(firstStudent.Id, firstPackage.Id, now),
            Grant(firstStudent.Id, firstPackage.Id, now.AddMinutes(1)),
            Grant(firstStudent.Id, secondPackage.Id, now),
            Grant(secondStudent.Id, firstPackage.Id, now, isGift: true));
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(teacherUser.Id, null, null), CancellationToken.None);

        Assert.True(response.Success);
        var physics = Assert.Single(response.Data!.Packages, packageSummary => packageSummary.PackageId == firstPackage.Id);
        Assert.Equal(1, physics.Package.Purchased);
        Assert.Equal(1, physics.Package.Gifts);
        Assert.Equal(1, physics.PurchasedStudents);
        Assert.Equal(1, physics.GiftStudents);
        Assert.Equal(2, physics.TotalStudents);
        var combination = Assert.Single(response.Data.PackageCombinations);
        Assert.Equal(1, combination.StudentsCount);
        Assert.Equal(2, combination.PackageIds.Count);
    }

    [Fact]
    public async Task Summary_respects_teacher_date_scope_and_ignores_cancelled_grants()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Scoped Teacher", "01082000001");
        var otherTeacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Other Teacher", "01082000002");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01082000003");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var otherTeacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = otherTeacherUser.Id, User = otherTeacherUser };
        var package = PackageFor(teacher, "رياضيات");
        var otherPackage = PackageFor(otherTeacher, "أحياء");
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        db.TeacherProfiles.AddRange(teacher, otherTeacher);
        db.Packages.AddRange(package, otherPackage);
        db.StudentAccessGrants.AddRange(
            Grant(student.Id, package.Id, from.AddDays(-1)),
            Grant(student.Id, package.Id, from.AddDays(1), cancelled: true),
            Grant(student.Id, otherPackage.Id, from.AddDays(1)));
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(teacherUser.Id, from, from.AddDays(2)), CancellationToken.None);

        Assert.True(response.Success);
        var packageSummary = Assert.Single(response.Data!.Packages);
        Assert.Equal(package.Id, packageSummary.PackageId);
        Assert.Equal(0, packageSummary.TotalStudents);
    }

    private static Package PackageFor(TeacherProfile teacher, string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Test",
        Price = 100,
        TargetGrade = "SecondaryGrade3",
        SubjectId = Guid.NewGuid(),
        TeacherId = teacher.Id,
        Teacher = teacher
    };

    private static StudentAccessGrant Grant(Guid userId, Guid packageId, DateTime grantedAt, bool isGift = false, bool cancelled = false) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PackageId = packageId,
        GrantType = CodeType.Package,
        GrantedAt = grantedAt,
        GiftRecipientId = isGift ? Guid.NewGuid() : null,
        CancelledAt = cancelled ? grantedAt.AddMinutes(1) : null,
        IsActive = !cancelled
    };
}
