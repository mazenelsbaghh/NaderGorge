using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using Xunit;

namespace NaderGorge.Application.Tests;

public sealed class ContentSummaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Summary_rejects_non_positive_date_ranges(int toOffsetMinutes)
    {
        await using var db = TestAppDbContextFactory.Create();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(null, from, from.AddMinutes(toOffsetMinutes)), CancellationToken.None);

        Assert.False(response.Success);
    }

    [Fact]
    public async Task Summary_date_scope_translates_on_relational_provider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var teacherUser = UserFor("Relational teacher", "01080500001");
        var student = UserFor("Relational student", "01080500002");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Physics", NormalizedName = "PHYSICS" };
        var package = new Package { Id = Guid.NewGuid(), Name = "Grade 12", Subject = subject, Teacher = teacher };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term", Package = package };
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        db.AddRange(teacher, subject, package, term);
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            User = student,
            GrantType = CodeType.Term,
            TermId = term.Id,
            GrantedAt = from,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(teacherUser.Id, from, from.AddDays(1)), CancellationToken.None);

        Assert.True(response.Success);
        var packageSummary = Assert.Single(response.Data!.Packages);
        Assert.Equal((1, 0), (packageSummary.Term.Purchased, packageSummary.Term.Gifts));
    }

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

    [Fact]
    public async Task Summary_purchase_wins_and_gift_students_are_exclusive_within_each_scope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01083000001");
        var mixedStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Mixed", "01083000002");
        var giftOnlyStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Gift only", "01083000003");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var package = PackageFor(teacher, "علوم");
        var firstTerm = new Term { Id = Guid.NewGuid(), Title = "الأول", PackageId = package.Id, Package = package };
        var secondTerm = new Term { Id = Guid.NewGuid(), Title = "الثاني", PackageId = package.Id, Package = package };
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "الوحدة", TermId = firstTerm.Id, Term = firstTerm };
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "الحصة", ContentSectionId = section.Id, ContentSection = section };
        var now = DateTime.UtcNow;

        db.AddRange(teacher, package, firstTerm, secondTerm, section, lesson);
        db.StudentAccessGrants.AddRange(
            TargetGrant(mixedStudent.Id, CodeType.Term, firstTerm.Id, now, isGift: true),
            TargetGrant(mixedStudent.Id, CodeType.Term, secondTerm.Id, now.AddMinutes(1)),
            TargetGrant(giftOnlyStudent.Id, CodeType.Lesson, lesson.Id, now, isGift: true));
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(teacherUser.Id, null, null), CancellationToken.None);

        Assert.True(response.Success);
        var packageSummary = Assert.Single(response.Data!.Packages);
        Assert.Equal((1, 0), (packageSummary.Term.Purchased, packageSummary.Term.Gifts));
        Assert.Equal((0, 1), (packageSummary.Lesson.Purchased, packageSummary.Lesson.Gifts));
        Assert.Equal((1, 1, 2),
            (packageSummary.PurchasedStudents, packageSummary.GiftStudents, packageSummary.TotalStudents));
    }

    [Fact]
    public async Task Summary_counts_expired_historical_grants_but_excludes_cancelled_and_end_boundary()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01084000001");
        var expiredStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Expired", "01084000002");
        var inactiveStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Inactive", "01084000003");
        var cancelledStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Cancelled", "01084000004");
        var boundaryStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Boundary", "01084000005");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var package = PackageFor(teacher, "تاريخ");
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(2);

        db.AddRange(teacher, package);
        var expiredGrant = Grant(expiredStudent.Id, package.Id, from);
        expiredGrant.ExpiresAt = from.AddHours(1);
        var inactiveGrant = Grant(inactiveStudent.Id, package.Id, from.AddDays(1));
        inactiveGrant.IsActive = false;
        db.StudentAccessGrants.AddRange(
            expiredGrant,
            inactiveGrant,
            Grant(cancelledStudent.Id, package.Id, from.AddHours(1), cancelled: true),
            Grant(boundaryStudent.Id, package.Id, to));
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(teacherUser.Id, from, to), CancellationToken.None);

        Assert.True(response.Success);
        var packageSummary = Assert.Single(response.Data!.Packages);
        Assert.Equal((2, 0, 2),
            (packageSummary.PurchasedStudents, packageSummary.GiftStudents, packageSummary.TotalStudents));
    }

    [Fact]
    public async Task Summary_admin_teacher_id_returns_only_selected_teacher_packages()
    {
        await using var db = TestAppDbContextFactory.Create();
        var firstTeacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "First teacher", "01085000001");
        var secondTeacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Second teacher", "01085000002");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01085000003");
        var firstTeacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = firstTeacherUser.Id, User = firstTeacherUser };
        var secondTeacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = secondTeacherUser.Id, User = secondTeacherUser };
        var firstPackage = PackageFor(firstTeacher, "فيزياء");
        var secondPackage = PackageFor(secondTeacher, "كيمياء");

        db.AddRange(firstTeacher, secondTeacher, firstPackage, secondPackage);
        db.StudentAccessGrants.AddRange(
            Grant(student.Id, firstPackage.Id, DateTime.UtcNow),
            Grant(student.Id, secondPackage.Id, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(null, null, null, secondTeacher.Id), CancellationToken.None);

        Assert.True(response.Success);
        var packageSummary = Assert.Single(response.Data!.Packages);
        Assert.Equal(secondPackage.Id, packageSummary.PackageId);
    }

    [Fact]
    public async Task Summary_video_scope_grant_does_not_inflate_package_acquisition_totals()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "01086000001");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01086000002");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var package = PackageFor(teacher, "جغرافيا");

        db.AddRange(teacher, package);
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Video,
            VideoTypeId = Guid.NewGuid(),
            PackageId = package.Id,
            GrantedAt = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var response = await new GetContentSummaryQueryHandler(db)
            .Handle(new GetContentSummaryQuery(teacherUser.Id, null, null), CancellationToken.None);

        Assert.True(response.Success);
        var packageSummary = Assert.Single(response.Data!.Packages);
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

    private static User UserFor(string fullName, string phoneNumber) => new()
    {
        Id = Guid.NewGuid(),
        FullName = fullName,
        PhoneNumber = phoneNumber,
        PasswordHash = "test-hash"
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

    private static StudentAccessGrant TargetGrant(
        Guid userId,
        CodeType type,
        Guid targetId,
        DateTime grantedAt,
        bool isGift = false)
    {
        var grant = new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GrantType = type,
            GrantedAt = grantedAt,
            GiftRecipientId = isGift ? Guid.NewGuid() : null,
            IsActive = true
        };

        switch (type)
        {
            case CodeType.Term:
                grant.TermId = targetId;
                break;
            case CodeType.Lesson:
                grant.LessonId = targetId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported test grant type.");
        }

        return grant;
    }
}
