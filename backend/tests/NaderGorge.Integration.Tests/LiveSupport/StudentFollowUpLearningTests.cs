using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.Reporting;
using NaderGorge.Application.Features.StudentFollowUp;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed partial class StudentFollowUpPostgresTests
{
    [Fact]
    public async Task ExactStudentReportIncludesUnwatchedPartsAndAllAccessibleCourseLessons()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        seed.Student.FullName = "أحمد محمد";
        var namesake = User("أحمد محمد");
        var nextLesson = new Lesson { Title = "الحصة التالية", ContentSectionId = seed.Lesson.ContentSectionId, Order = 2 };
        var unwatchedPart = new LessonVideo { Title = "جزء لم يشاهده", LessonId = seed.Lesson.Id,
            VideoTypeId = seed.Video.VideoTypeId, Provider = "youtube", ProviderVideoId = "part", IsActive = true };
        fixture.Db.AddRange(unwatchedPart, new LessonVideo { Title = "فيديو الحصة التالية", Lesson = nextLesson,
            VideoTypeId = seed.Video.VideoTypeId, Provider = "youtube", ProviderVideoId = "next", IsActive = true },
            new StudentProfile { User = namesake, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary },
            new StudentAccessGrant { User = namesake, GrantType = CodeType.Package, PackageId = seed.Package.Id, GrantedAt = Now.AddDays(-2) },
            new VideoWatchEvent { UserId = seed.Student.Id, LessonVideoId = seed.Video.Id, LearningWatchedSeconds = 100, LearningDurationSeconds = 100 });
        await fixture.Db.SaveChangesAsync();
        var reader = Reader(fixture.Db);

        var choices = await reader.SearchStudentsAsync(new(seed.Teacher.Id, seed.Package.Id, "أحمد"), default);
        Assert.Equal(2, choices.Students.Count);
        Assert.Contains(choices.Students, student => student.Id == seed.Student.Id);
        Assert.Contains(choices.Students, student => student.Id == namesake.Id);
        var filter = Filter(seed) with { LessonIds = [], StudentId = seed.Student.Id };
        var report = await reader.ReadAsync(filter, false, default);

        Assert.Equal(2, report.TotalRows);
        Assert.All(report.Rows, row => Assert.Equal(seed.Student.Id, row.StudentId));
        var parts = report.Rows.Single(row => row.LessonId == seed.Lesson.Id).Videos;
        Assert.Equal("Completed", parts.Single(video => video.Id == seed.Video.Id).State);
        Assert.Equal("NotStarted", parts.Single(video => video.Id == unwatchedPart.Id).State);
        Assert.Equal("NotStarted", Assert.Single(report.Rows.Single(row => row.LessonId == nextLesson.Id).Videos).State);
        var bytes = await new StudentFollowUpExportService(fixture.Db).ExportAsync(report, filter, seed.Teacher.UserId, default);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var detail = workbook.Worksheet("تفاصيل الفيديوهات");
        Assert.Equal(4, detail.LastRowUsed()!.RowNumber());
        Assert.Contains(detail.Column(5).CellsUsed(), cell => cell.GetString() == "جزء لم يشاهده");
    }

    [Theory]
    [InlineData("present", "Started")]
    [InlineData("absent", "NotStarted")]
    public async Task LessonAttendanceUsesOnlyTheSelectedLesson(string attendance, string expectedState)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var otherLesson = new Lesson { Title = "حصة أخرى", ContentSectionId = seed.Lesson.ContentSectionId };
        var otherVideo = new LessonVideo { Title = "فيديو آخر", Lesson = otherLesson, VideoTypeId = seed.Video.VideoTypeId,
            Provider = "youtube", ProviderVideoId = "other", IsActive = true };
        fixture.Db.AddRange(otherVideo, new VideoWatchEvent { UserId = seed.Student.Id, LessonVideoId = seed.Video.Id, LearningWatchedSeconds = 10, LearningDurationSeconds = 100 });
        await fixture.Db.SaveChangesAsync();

        var report = await Reader(fixture.Db).ReadAsync(Filter(seed) with { LessonIds = [seed.Lesson.Id, otherLesson.Id], Attendance = attendance }, false, default);

        var row = Assert.Single(report.Rows);
        Assert.Equal(attendance == "present" ? seed.Lesson.Id : otherLesson.Id, row.LessonId);
        Assert.Equal(expectedState, row.State);
    }

    [Fact]
    public async Task ReportScopeRejectsOtherTeachersAndRequiresReportsPermission()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var foreign = await Seed(fixture.Db);
        var reader = Reader(fixture.Db);
        TeacherReportsController Reports(Guid userId) => new(null!, null!, new ReportQueryService(fixture.Db), null!)
        { ControllerContext = Controller(fixture.Db, userId).ControllerContext };

        Assert.IsType<BadRequestObjectResult>(await Reports(seed.Teacher.UserId).LearningReport(Filter(foreign), reader, default));
        Assert.IsType<BadRequestObjectResult>(await Reports(seed.Teacher.UserId).LearningStudents(new(foreign.Teacher.Id, foreign.Package.Id, "طالب"), reader, default));
        var own = await reader.ReadAsync(Filter(seed) with { StudentId = foreign.Student.Id, LessonIds = [] }, false, default);
        Assert.Empty(own.Rows);
        seed.Grant.CancelledAt = Now.AddDays(-1);
        await fixture.Db.SaveChangesAsync();
        Assert.Empty((await reader.SearchStudentsAsync(new(seed.Teacher.Id, seed.Package.Id, "طالب"), default)).Students);

        var staff = User("مساعد تقارير");
        var membership = new TeacherStaffMember { User = staff, TeacherId = foreign.Teacher.Id,
            CreatedByTeacherUserId = foreign.Teacher.UserId, PermissionKeys = "students", IsActive = true };
        fixture.Db.Add(membership);
        await fixture.Db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await Reports(staff.Id).LearningReport(Filter(foreign), reader, default));
        membership.PermissionKeys = "reports";
        await fixture.Db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await Reports(staff.Id).LearningReport(Filter(foreign) with { TeacherId = seed.Teacher.Id }, reader, default));
    }
}
