using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Reporting;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed partial class StudentFollowUpPostgresTests
{
    [Fact]
    public async Task CourseSummaryIncludesOnlySelectedCourseAndStageAndPreservesHistoricalGrades()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db);
        var otherCourse = await Seed(fixture.Db);
        own.Student.FullName = "=أحمد محمد";
        otherCourse.Student.FullName = "طالب كورس آخر";
        otherCourse.Package.TeacherId = own.Teacher.Id;
        var primaryStudent = User("طالب مرحلة أخرى");
        var exam = new Exam { Title = "امتحان الحصة", TotalScore = 99, CreatedByTeacherId = own.Teacher.Id };
        own.Lesson.ExamId = exam.Id;
        var homework = new Homework { Title = "واجب الحصة", LessonId = own.Lesson.Id, TotalScore = 99 };
        fixture.Db.AddRange(
            new StudentProfile { User = primaryStudent, EducationStage = EducationStage.Primary, GradeLevel = GradeLevel.PrimaryGrade1 },
            new StudentAccessGrant { User = primaryStudent, GrantType = CodeType.Package, PackageId = own.Package.Id },
            new StudentExamAttempt { UserId = own.Student.Id, Exam = exam, Evaluation = "ممتاز", ScoreAchieved = 8,
                DefinitionSnapshotJson = (AssessmentDefinitionSnapshot.FromExam(exam) with { TotalScore = 10 }).ToJson() },
            new HomeworkSubmission { StudentId = own.Student.Id, Homework = homework, Status = SubmissionStatus.Graded,
                OverallScore = 7, TotalScoreSnapshot = 10 },
            new VideoWatchEvent { UserId = own.Student.Id, LessonVideoId = own.Video.Id, LearningWatchedSeconds = 1 });
        await fixture.Db.SaveChangesAsync();

        var export = await new StudentLedgerExportService(fixture.Db).ExportCourseSummaryAsync(own.Teacher.Id, own.Teacher.UserId,
            new(EducationStage.Secondary, own.Package.Id), default);

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("ملخص", sheet.Name);
        Assert.True(sheet.RightToLeft);
        Assert.Equal(5, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(9, sheet.LastColumnUsed()!.ColumnNumber());
        Assert.Equal(own.Student.FullName, sheet.Cell(5, 1).GetString());
        Assert.False(sheet.Cell(5, 1).HasFormula);
        Assert.Equal("شاهد", sheet.Cell(5, 7).GetString());
        Assert.Equal("8 / 10", sheet.Cell(5, 8).GetString());
        Assert.Equal("7 / 10", sheet.Cell(5, 9).GetString());
        Assert.DoesNotContain(sheet.CellsUsed(), cell => cell.GetString().Contains(primaryStudent.FullName));
        Assert.DoesNotContain(sheet.CellsUsed(), cell => cell.GetString().Contains(otherCourse.Student.FullName));
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync(log => log.Action == "StudentCourseSummaryExported" && log.PerformedByUserId == own.Teacher.UserId));
        if (Environment.GetEnvironmentVariable("COURSE_SUMMARY_PREVIEW_PATH") is { } previewPath)
            await File.WriteAllBytesAsync(previewPath, export.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CourseSummaryDistinguishesUnwatchedPendingAndLatestAttemptWithoutInventingZero(bool pending)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db);
        var exam = new Exam { Title = "امتحان", TotalScore = 10, CreatedByTeacherId = own.Teacher.Id };
        own.Lesson.ExamId = exam.Id;
        var homework = new Homework { Title = "واجب", LessonId = own.Lesson.Id, TotalScore = 10,
            Questions = [new HomeworkQuestion { BodyText = "سؤال", PointsActive = 10 }] };
        fixture.Db.AddRange(exam, homework, new VideoWatchEvent { UserId = own.Student.Id, LessonVideoId = own.Video.Id });
        if (pending)
        {
            fixture.Db.AddRange(new StudentExamAttempt { UserId = own.Student.Id, Exam = exam, Evaluation = "ممتاز", ScoreAchieved = 9,
                    StartedAt = Now.AddDays(-2), DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson() },
                new StudentExamAttempt { UserId = own.Student.Id, Exam = exam, Evaluation = "قيد التصحيح", ScoreAchieved = 0, StartedAt = Now.AddDays(-1) },
                new HomeworkSubmission { StudentId = own.Student.Id, Homework = homework, Status = SubmissionStatus.PendingReview });
        }
        await fixture.Db.SaveChangesAsync();

        var export = await new StudentLedgerExportService(fixture.Db).ExportCourseSummaryAsync(own.Teacher.Id, own.Teacher.UserId, new(EducationStage.Secondary, own.Package.Id), default);

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("لم يشاهد", sheet.Cell(5, 7).GetString());
        Assert.Equal(pending ? "بانتظار التصحيح" : "لم يمتحن", sheet.Cell(5, 8).GetString());
        Assert.Equal(pending ? "بانتظار التصحيح" : "لم يسلم", sheet.Cell(5, 9).GetString());
    }

    [Fact]
    public async Task CourseSummaryIncludesEveryAssessmentAndDoesNotUnlockSiblingLessonsWithPartialGrants()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db);
        own.Grant.GrantType = CodeType.Lesson; own.Grant.LessonId = own.Lesson.Id;
        var secondLesson = new Lesson { Title = "حصة غير مشتراة", ContentSectionId = own.Lesson.ContentSectionId, Order = 2 };
        var secondVideo = new LessonVideo { Lesson = secondLesson, Title = "فيديو آخر", Provider = "youtube", ProviderVideoId = "second", VideoTypeId = own.Video.VideoTypeId, IsActive = true };
        var firstExam = new Exam { Title = "امتحان الحصة", TotalScore = 10, CreatedByTeacherId = own.Teacher.Id };
        var videoExam = new Exam { Title = "امتحان الفيديو", TotalScore = 10, CreatedByTeacherId = own.Teacher.Id, LessonVideoId = own.Video.Id };
        own.Lesson.ExamId = firstExam.Id; own.Video.ExamId = videoExam.Id;
        fixture.Db.AddRange(secondVideo, firstExam, videoExam, new VideoPlaybackSession { UserId = own.Student.Id,
            LessonVideoId = own.Video.Id, HasRegisteredView = true, SessionToken = Guid.NewGuid().ToString(), EncryptionKey = "test", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await fixture.Db.SaveChangesAsync();

        var export = await new StudentLedgerExportService(fixture.Db).ExportCourseSummaryAsync(own.Teacher.Id, own.Teacher.UserId, new(EducationStage.Secondary, own.Package.Id), default);

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("شاهد", sheet.Cell(5, 7).GetString());
        Assert.Contains("امتحان الفيديو: لم يمتحن", sheet.Cell(5, 8).GetString());
        Assert.Contains("امتحان الحصة: لم يمتحن", sheet.Cell(5, 8).GetString());
        Assert.Equal("لا يوجد واجب", sheet.Cell(5, 9).GetString());
        Assert.Equal("لا توجد فيديوهات متاحة ضمن اشتراكه", sheet.Cell(5, 10).GetString());
        Assert.Equal("لا يوجد امتحان", sheet.Cell(5, 11).GetString());
    }

    [Fact]
    public async Task SummaryEndpointRejectsForeignCoursesAndStaffWithoutReportsPermission()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db); var foreign = await Seed(fixture.Db);
        TeacherReportsController Endpoint(Guid actorId) => new(null!, null!, new ReportQueryService(fixture.Db), new StudentLedgerExportService(fixture.Db))
        { ControllerContext = Controller(fixture.Db, actorId).ControllerContext };
        Assert.IsType<BadRequestObjectResult>(await Endpoint(own.Teacher.UserId).ExportSummary(new(EducationStage.Secondary, foreign.Package.Id), default));
        var staff = User("مساعد");
        var membership = new TeacherStaffMember { User = staff, TeacherId = own.Teacher.Id, IsActive = true,
            CreatedByTeacherUserId = own.Teacher.UserId, PermissionKeys = "students" };
        fixture.Db.Add(membership); await fixture.Db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await Endpoint(staff.Id).ExportSummary(new(EducationStage.Secondary, own.Package.Id), default));
        membership.PermissionKeys = "reports"; await fixture.Db.SaveChangesAsync();
        Assert.IsType<FileContentResult>(await Endpoint(staff.Id).ExportSummary(new(EducationStage.Secondary, own.Package.Id), default));
    }

    [Fact]
    public async Task CourseSummaryDoesNotTreatAnotherTeachersVideoTypeCodeAsCourseEnrollment()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db); var foreign = await Seed(fixture.Db);
        foreign.Student.FullName = "طالب كود مدرس آخر";
        foreign.Grant.GrantType = CodeType.Video;
        foreign.Grant.PackageId = null; foreign.Grant.VideoTypeId = own.Video.VideoTypeId;
        foreign.Grant.AccessCode = new AccessCode { CodeHash = Guid.NewGuid().ToString("N"), CodeGroup = new CodeGroup
        { TeacherId = foreign.Teacher.Id, CreatedByUserId = foreign.Teacher.UserId, CodeType = CodeType.Video, VideoTypeId = own.Video.VideoTypeId } };
        fixture.Db.Add(foreign.Grant.AccessCode);
        await fixture.Db.SaveChangesAsync();

        var export = await new StudentLedgerExportService(fixture.Db).ExportCourseSummaryAsync(own.Teacher.Id, own.Teacher.UserId,
            new(EducationStage.Secondary, own.Package.Id), default);

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        Assert.Equal(5, workbook.Worksheet(1).LastRowUsed()!.RowNumber());
        Assert.DoesNotContain(workbook.Worksheet(1).CellsUsed(), cell => cell.GetString().Contains(foreign.Student.FullName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999)]
    public async Task CourseSummaryRequiresAnExplicitValidStage(int? stage)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db);
        await Assert.ThrowsAsync<ArgumentException>(() => new StudentLedgerExportService(fixture.Db).ExportCourseSummaryAsync(
            own.Teacher.Id, own.Teacher.UserId, new(stage is { } code ? (EducationStage)code : null, own.Package.Id), default));
    }
}
