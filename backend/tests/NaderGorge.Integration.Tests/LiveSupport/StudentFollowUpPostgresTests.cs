using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.StudentFollowUp;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class StudentFollowUpPostgresTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc);

    // Boundary cases: unpublished lesson age, publication before/after purchase, seven-day cutoff, recent purchase.
    [Theory]
    [InlineData(10, 20, 10)]
    [InlineData(10, 2, 2)]
    [InlineData(3, 20, 3)]
    [InlineData(7, 7, 7)]
    [InlineData(6, 20, 6)]
    [InlineData(0, 20, 0)]
    public async Task WaitingStartsAtLaterOfVideoOpeningAndEffectiveAccess(int openedDays, int purchasedDays, int expectedDays)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        await fixture.Db.LessonVideos.Where(v => v.Id == seed.Video.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(v => v.StudentOpenedAtUtc, Now.AddDays(-openedDays)));
        await fixture.Db.StudentAccessGrants.Where(g => g.Id == seed.Grant.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(g => g.GrantedAt, Now.AddDays(-purchasedDays)));
        var reader = Reader(fixture.Db);
        var report = await reader.ReadAsync(Filter(seed), false, default);

        var row = Assert.Single(report.Rows);
        Assert.Equal(expectedDays, row.DaysWaiting);
        Assert.Equal("NotStarted", row.State);
        Assert.Equal(Now.AddDays(-expectedDays), row.AvailableSinceUtc);
        var late = await reader.ReadAsync(Filter(seed) with { MinimumDays = 7 }, false, default);
        Assert.Equal(expectedDays >= 7 ? 1 : 0, late.TotalRows);
    }

    [Fact]
    public async Task SelectedLessonsKeepUnstartedStudentsAndRequireEveryAccessiblePartForCompletion()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var second = new Lesson { Title = "الحصة الثانية", ContentSectionId = seed.Lesson.ContentSectionId, Order = 2 };
        var part = new LessonVideo { Title = "الجزء الثاني", LessonId = seed.Lesson.Id, VideoTypeId = seed.Video.VideoTypeId, Provider = "youtube", ProviderVideoId = "part", IsActive = true };
        var secondVideo = new LessonVideo { Title = "الحصة الثانية", Lesson = second, VideoTypeId = seed.Video.VideoTypeId, Provider = "youtube", ProviderVideoId = "second", IsActive = true };
        fixture.Db.AddRange(part, secondVideo);
        fixture.Db.VideoWatchEvents.Add(new VideoWatchEvent { UserId = seed.Student.Id, LessonVideoId = seed.Video.Id, LearningWatchedSeconds = 100, LearningDurationSeconds = 100, ActualWatchedSeconds = 50 });
        await fixture.Db.SaveChangesAsync();
        var reader = Reader(fixture.Db);
        var filter = Filter(seed) with { LessonIds = [seed.Lesson.Id, second.Id], State = "All" };

        var report = await reader.ReadAsync(filter, false, default);

        Assert.Equal(2, report.TotalRows);
        Assert.Equal(1, report.TotalStudents);
        var first = Assert.Single(report.Rows, r => r.LessonId == seed.Lesson.Id);
        Assert.Equal(("Started", 1, 2), (first.State, first.CompletedVideos, first.TotalVideos));
        Assert.Equal("NotStarted", report.Rows.Single(r => r.LessonId == second.Id).State);
        fixture.Db.VideoWatchEvents.Add(new VideoWatchEvent { UserId = seed.Student.Id, LessonVideoId = part.Id, LearningWatchedSeconds = 100, LearningDurationSeconds = 100 });
        await fixture.Db.SaveChangesAsync();
        var completed = await reader.ReadAsync(filter with { State = "Completed" }, false, default);
        Assert.Equal(100, Assert.Single(completed.Rows).ProgressPercentage);
        var missingSecond = await reader.ReadAsync(filter with { LessonIds = [second.Id], State = "NotStarted" }, false, default);
        Assert.Equal(second.Id, Assert.Single(missingSecond.Rows).LessonId);
    }

    [Fact]
    public async Task ScopeFiltersExcludeCancelledExpiredHiddenForeignAndWrongGradeContent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var other = await Seed(fixture.Db);
        var reader = Reader(fixture.Db);
        Assert.Empty((await reader.ReadAsync(Filter(seed) with { Grade = GradeLevel.SecondBaccalaureate }, false, default)).Rows);
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(Filter(seed) with { PackageId = other.Package.Id }, false, default));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(Filter(seed) with { LessonIds = [other.Lesson.Id] }, false, default));
        seed.Grant.CancelledAt = Now.AddDays(-1);
        await fixture.Db.SaveChangesAsync();
        Assert.Empty((await reader.ReadAsync(Filter(seed), false, default)).Rows);
        seed.Grant.CancelledAt = null; seed.Grant.ExpiresAt = Now;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty((await reader.ReadAsync(Filter(seed), false, default)).Rows);
        seed.Grant.ExpiresAt = null;
        seed.Video.ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty((await reader.ReadAsync(Filter(seed), false, default)).Rows);
    }

    [Fact]
    public async Task UnknownHistoricalOpeningIsNeverReplacedByLessonCreationAndPlaybackEvidenceIsLabelled()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        await fixture.Db.LessonVideos.Where(v => v.Id == seed.Video.Id).ExecuteUpdateAsync(update => update.SetProperty(v => v.StudentOpenedAtUtc, (DateTime?)null));
        var reader = Reader(fixture.Db);
        var report = await reader.ReadAsync(Filter(seed), false, default);
        Assert.Null(Assert.Single(report.Rows).DaysWaiting);
        Assert.Equal(1, report.UnknownAvailability);
        Assert.Empty((await reader.ReadAsync(Filter(seed) with { MinimumDays = 7 }, false, default)).Rows);
        fixture.Db.VideoPlaybackSessions.Add(new VideoPlaybackSession { UserId = seed.Student.Id, LessonVideoId = seed.Video.Id,
            SessionToken = Guid.NewGuid().ToString(), EncryptionKey = "test", ExpiresAt = Now.AddDays(1), HasRegisteredView = true, CreatedAt = Now.AddDays(-8) });
        await fixture.Db.SaveChangesAsync();
        var known = await reader.ReadAsync(Filter(seed), false, default);
        Assert.Equal((8, "RecordedPlayback", "Started"), (Assert.Single(known.Rows).DaysWaiting, known.Rows[0].AvailabilitySource, known.Rows[0].State));
        Assert.Empty((await reader.ReadAsync(Filter(seed) with { State = "NotStarted" }, false, default)).Rows);
    }

    [Theory]
    [InlineData(CodeType.Term)]
    [InlineData(CodeType.Month)]
    [InlineData(CodeType.Lesson)]
    [InlineData(CodeType.Video)]
    public async Task EffectiveAccessIncludesEveryPurchaseLevelAndUsesEarliestOverlappingGrant(CodeType scope)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var section = await fixture.Db.ContentSections.FindAsync(seed.Lesson.ContentSectionId);
        seed.Grant.GrantType = scope;
        seed.Grant.PackageId = null;
        seed.Grant.TermId = scope == CodeType.Term ? section!.TermId : null;
        seed.Grant.ContentSectionId = scope == CodeType.Month ? section!.Id : null;
        seed.Grant.LessonId = scope == CodeType.Lesson ? seed.Lesson.Id : null;
        seed.Grant.LessonVideoId = scope == CodeType.Video ? seed.Video.Id : null;
        fixture.Db.StudentAccessGrants.Add(new StudentAccessGrant { UserId = seed.Student.Id,
            GrantType = CodeType.Package, PackageId = seed.Package.Id, GrantedAt = Now.AddDays(-2) });
        await fixture.Db.SaveChangesAsync();
        await fixture.Db.LessonVideos.Where(v => v.Id == seed.Video.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(v => v.StudentOpenedAtUtc, Now.AddDays(-10)));

        var report = await Reader(fixture.Db).ReadAsync(Filter(seed), false, default);

        Assert.Equal(10, Assert.Single(report.Rows).DaysWaiting);
        Assert.Equal(1, report.TotalStudents);
    }

    [Fact]
    public async Task OpeningStampChangesOnlyOnPublicationAndReopeningNotMetadataEdits()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var original = seed.Video.StudentOpenedAtUtc;
        Assert.NotNull(original);
        seed.Video.Title = "عنوان معدل";
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(original, seed.Video.StudentOpenedAtUtc);
        seed.Video.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        seed.Video.StudentOpenedAtUtc = Now.AddDays(-20);
        await fixture.Db.SaveChangesAsync();
        seed.Video.IsActive = true;
        await fixture.Db.SaveChangesAsync();
        Assert.True(seed.Video.StudentOpenedAtUtc > Now.AddDays(-1));
    }

    [Fact]
    public async Task TeacherEndpointIgnoresInjectedTeacherAndStaffRequiresStudentPermission()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db); var other = await Seed(fixture.Db);
        var controller = Controller(fixture.Db, seed.Teacher.UserId);
        var response = Assert.IsType<OkObjectResult>(await controller.FollowUp(Filter(seed) with { TeacherId = other.Teacher.Id }, Reader(fixture.Db), default));
        Assert.NotNull(response.Value);
        Assert.IsType<BadRequestObjectResult>(await controller.FollowUp(Filter(other), Reader(fixture.Db), default));
        var staff = User("مساعد");
        fixture.Db.TeacherStaffMembers.Add(new TeacherStaffMember { User = staff, TeacherId = seed.Teacher.Id, CreatedByTeacherUserId = seed.Teacher.UserId, PermissionKeys = "content", IsActive = true });
        await fixture.Db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await Controller(fixture.Db, staff.Id).FollowUp(Filter(seed), Reader(fixture.Db), default));
        var member = await fixture.Db.TeacherStaffMembers.SingleAsync(m => m.UserId == staff.Id);
        member.PermissionKeys = "students";
        await fixture.Db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await Controller(fixture.Db, staff.Id).FollowUp(Filter(seed), Reader(fixture.Db), default));
    }

    [Fact]
    public async Task ExportUsesAllFilteredRowsBeyondCurrentPageAndWritesContactsAsText()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        seed.Student.FullName = "=student";
        var second = User("طالب آخر");
        fixture.Db.StudentProfiles.Add(new StudentProfile { User = second, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary });
        fixture.Db.StudentAccessGrants.Add(new StudentAccessGrant { User = second, PackageId = seed.Package.Id, GrantType = CodeType.Package, GrantedAt = Now.AddDays(-20) });
        await fixture.Db.SaveChangesAsync();
        var filter = Filter(seed) with { PageSize = 1 };
        var reader = Reader(fixture.Db);
        Assert.Single((await reader.ReadAsync(filter, false, default)).Rows);
        var report = await reader.ReadAsync(filter, true, default);
        Assert.Equal(2, report.Rows.Count);
        var bytes = await new StudentFollowUpExportService(fixture.Db).ExportAsync(report, filter, seed.Teacher.UserId, default);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);
        Assert.True(sheet.RightToLeft);
        Assert.Equal(8, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal("=student", sheet.Cell(7, 1).GetString());
        Assert.False(sheet.Cell(7, 1).HasFormula);
        Assert.Equal(seed.Student.PhoneNumber, sheet.Cell(7, 2).GetString());
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync(a => a.Action == "StudentFollowUpExported" && a.PerformedByUserId == seed.Teacher.UserId));
    }

    private static StudentFollowUpReader Reader(AppDbContext db) => new(db, new AcademicScopeService(db), new FrozenTime());
    private static StudentFollowUpFilter Filter(SeedData seed) => new(seed.Teacher.Id, seed.Package.Id, [seed.Lesson.Id], State: "All");
    private sealed class FrozenTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed record SeedData(TeacherProfile Teacher, User Student, Package Package, Lesson Lesson, LessonVideo Video, StudentAccessGrant Grant);
    private static async Task<SeedData> Seed(AppDbContext db)
    {
        var teacher = new TeacherProfile { User = User("مدرس") };
        var student = User("طالب");
        var package = new Package { Name = "الكورس", Teacher = teacher, Subject = new Subject { Name = "التاريخ", NormalizedName = Guid.NewGuid().ToString() } };
        var lesson = new Lesson { Title = "الحصة الأولى", CreatedAt = Now.AddDays(-100), ContentSection = new ContentSection { Title = "الشهر", Term = new Term { Title = "الترم", Package = package } } };
        var video = new LessonVideo { Title = "فيديو", Lesson = lesson, Provider = "youtube", ProviderVideoId = "source", VideoType = new VideoType { Name = "شرح", NormalizedName = Guid.NewGuid().ToString("N"), IsActive = true }, IsActive = true };
        var grant = new StudentAccessGrant { User = student, PackageId = package.Id, GrantType = CodeType.Package, GrantedAt = Now.AddDays(-20), IsActive = true };
        db.AddRange(video, grant, new StudentProfile { User = student, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary },
            new StudentFacingAcademicScope { OwnerType = StudentFacingScopeOwnerType.Package, OwnerId = package.Id, ScopeLevel = AcademicScopeLevel.PlatformWide });
        await db.SaveChangesAsync();
        return new(teacher, student, package, lesson, video, grant);
    }
    private static User User(string name) => new() { FullName = name, PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "test", IsActive = true };
    private static TeacherController Controller(AppDbContext db, Guid userId) => new(null!, db)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) } } };
}
