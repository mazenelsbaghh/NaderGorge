using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Content.Queries;
using NaderGorge.Application.Features.Admin.Teachers.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Admin;

public sealed class ContentEnrollmentTotalsTests
{
    [Fact]
    public async Task PackageStats_UsesHistoricalDistinctAcquisitionsAcrossAllFourContentLevels()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var content = await SeedContentAsync(db, "Historical Totals");
        var mixed = await TestAppDbContextFactory.SeedUserAsync(db, "Mixed Acquisition", "01091000001");
        var inactive = await TestAppDbContextFactory.SeedUserAsync(db, "Inactive Acquisition", "01091000002");
        var giftOnly = await TestAppDbContextFactory.SeedUserAsync(db, "Gift Acquisition", "01091000003");
        var cancelled = await TestAppDbContextFactory.SeedUserAsync(db, "Cancelled Acquisition", "01091000004");
        var now = DateTime.UtcNow;

        db.StudentAccessGrants.AddRange(
            new StudentAccessGrant { UserId = mixed.Id, GrantType = CodeType.Term, TermId = content.Term.Id, ExpiresAt = now.AddDays(-1), IsActive = true },
            new StudentAccessGrant { UserId = mixed.Id, GrantType = CodeType.Lesson, LessonId = content.Lesson.Id, IsActive = true },
            new StudentAccessGrant { UserId = inactive.Id, GrantType = CodeType.Month, ContentSectionId = content.Section.Id, IsActive = false },
            new StudentAccessGrant { UserId = giftOnly.Id, GrantType = CodeType.Package, PackageId = content.Package.Id, IsActive = true },
            new StudentAccessGrant { UserId = cancelled.Id, GrantType = CodeType.Package, PackageId = content.Package.Id, IsActive = false, CancelledAt = now });
        await db.SaveChangesAsync();

        var response = await new GetPackageStatsQueryHandler(db)
            .Handle(new GetPackageStatsQuery(content.Package.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(3, response.Data!.EnrolledStudentsCount);
    }

    [Fact]
    public async Task TeacherStudents_ReturnsDistinctEffectiveStudentsFromGranularTargets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var content = await SeedContentAsync(db, "Teacher Students");
        var otherTeacherContent = await SeedContentAsync(db, "Other Teacher Students");
        var multiGrantStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Multi Grant Student", "01092000001");
        var lessonStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Lesson Student", "01092000002");
        var expiredStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Expired Student", "01092000003");
        var cancelledStudent = await TestAppDbContextFactory.SeedUserAsync(db, "Cancelled Student", "01092000004");
        var now = DateTime.UtcNow;
        var secondSubject = new Subject { Name = "Second Teacher Subject", NormalizedName = Guid.NewGuid().ToString("N") };
        var secondPackage = new Package
        {
            Name = content.Package.Name,
            Subject = secondSubject,
            Teacher = content.Teacher,
            TeacherId = content.Teacher.Id,
            TargetGrade = "SecondaryGrade3",
            IsActive = true,
            Price = 80m
        };
        db.AddRange(secondSubject, secondPackage);

        db.StudentAccessGrants.AddRange(
            new StudentAccessGrant { UserId = multiGrantStudent.Id, GrantType = CodeType.Lesson, LessonId = content.Lesson.Id, GrantedAt = now.AddDays(-2), IsActive = true },
            new StudentAccessGrant { UserId = multiGrantStudent.Id, GrantType = CodeType.Term, TermId = content.Term.Id, GrantedAt = now.AddDays(-1), IsActive = true },
            new StudentAccessGrant { UserId = multiGrantStudent.Id, GrantType = CodeType.Package, PackageId = secondPackage.Id, GrantedAt = now.AddDays(-3), IsActive = true },
            new StudentAccessGrant { UserId = lessonStudent.Id, GrantType = CodeType.Lesson, LessonId = content.Lesson.Id, GrantedAt = now, IsActive = true },
            new StudentAccessGrant { UserId = expiredStudent.Id, GrantType = CodeType.Month, ContentSectionId = content.Section.Id, ExpiresAt = now.AddMinutes(-1), IsActive = true },
            new StudentAccessGrant { UserId = cancelledStudent.Id, GrantType = CodeType.Package, PackageId = content.Package.Id, CancelledAt = now, IsActive = false });
        db.VideoWatchEvents.AddRange(
            new VideoWatchEvent
            {
                UserId = multiGrantStudent.Id,
                LessonVideoId = content.Video.Id,
                LessonVideo = content.Video,
                ActualWatchedSeconds = 60,
                WatchCount = 3,
                CreatedAt = now.AddHours(-2)
            },
            new VideoWatchEvent
            {
                UserId = multiGrantStudent.Id,
                LessonVideoId = otherTeacherContent.Video.Id,
                LessonVideo = otherTeacherContent.Video,
                ActualWatchedSeconds = 120,
                WatchCount = 4,
                CreatedAt = now.AddHours(-1)
            });
        await db.SaveChangesAsync();

        var response = await new GetTeacherStudentsQueryHandler(db)
            .Handle(new GetTeacherStudentsQuery(content.Teacher.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.TotalCount);
        Assert.Equal(2, response.Data.Items.Count);
        var multiGrantRow = Assert.Single(response.Data.Items, item => item.StudentId == multiGrantStudent.Id);
        Assert.Equal(content.Package.Name, multiGrantRow.PackageName);
        Assert.Equal(content.Term.Price, multiGrantRow.Price);
        Assert.Equal(2, multiGrantRow.Packages.Count);
        Assert.Contains(multiGrantRow.Packages, package => package.PackageId == content.Package.Id);
        Assert.Contains(multiGrantRow.Packages, package => package.PackageId == secondPackage.Id);
        Assert.Equal(1, multiGrantRow.WatchedVideosCount);
        Assert.Equal(now.AddHours(-2), multiGrantRow.LastWatchedAt);
        Assert.DoesNotContain(response.Data.Items, item => item.StudentId == expiredStudent.Id || item.StudentId == cancelledStudent.Id);
    }

    private static async Task<ContentFixture> SeedContentAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        string prefix)
    {
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, $"{prefix} Teacher", $"01{Guid.NewGuid():N}"[..11]);
        var teacher = new TeacherProfile { UserId = teacherUser.Id, User = teacherUser };
        var subject = new Subject { Name = $"{prefix} Subject", NormalizedName = Guid.NewGuid().ToString("N") };
        var package = new Package { Name = $"{prefix} Package", Subject = subject, Teacher = teacher, TeacherId = teacher.Id, TargetGrade = "SecondaryGrade3", IsActive = true, Price = 100m };
        var term = new Term { Title = $"{prefix} Term", Package = package, PackageId = package.Id, Price = 60m };
        var section = new ContentSection { Title = $"{prefix} Section", Term = term, TermId = term.Id, Price = 40m };
        var lesson = new Lesson { Title = $"{prefix} Lesson", Summary = "Summary", ContentSection = section, ContentSectionId = section.Id, Price = 20m };
        var videoType = new VideoType { Name = $"{prefix} Type", NormalizedName = Guid.NewGuid().ToString("N") };
        var video = new LessonVideo { Title = $"{prefix} Video", Provider = "youtube", ProviderVideoId = Guid.NewGuid().ToString("N"), Lesson = lesson, LessonId = lesson.Id, VideoType = videoType, VideoTypeId = videoType.Id };
        db.AddRange(teacher, subject, package, term, section, lesson, videoType, video);
        await db.SaveChangesAsync();
        return new ContentFixture(teacher, package, term, section, lesson, video);
    }

    private sealed record ContentFixture(
        TeacherProfile Teacher,
        Package Package,
        Term Term,
        ContentSection Section,
        Lesson Lesson,
        LessonVideo Video);
}
