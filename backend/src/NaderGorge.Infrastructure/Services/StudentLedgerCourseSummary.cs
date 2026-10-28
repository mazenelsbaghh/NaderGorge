using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Reporting;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using System.Text.Json;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class StudentLedgerExportService
{
    private const int SummaryIdentityColumns = 6;

    public async Task<ReportExportDto> ExportCourseSummaryAsync(Guid teacherId, Guid actorUserId,
        StudentCourseSummaryFilter filter, CancellationToken ct)
    {
        if (filter.Stage is not { } stage || !Enum.IsDefined(stage) || filter.PackageId == Guid.Empty)
            throw new ArgumentException("اختر المرحلة والكورس أولًا.");
        var packages = await LoadPackagesAsync(teacherId, filter.PackageId, ct);
        var course = packages.SingleOrDefault() ?? throw new ArgumentException("الكورس غير موجود ضمن كورساتك.");
        var map = BuildPackageMap(packages);
        var grants = (await LoadGrantsAsync(teacherId, ct))
            .Where(grant => BelongsToPackage(grant, course.Id, map, packages)).ToList();
        var students = await LoadStudentsAsync(grants.Select(grant => grant.UserId).Distinct().ToArray(), new(filter.Stage), ct);
        var lessons = SummaryLessons(course);
        if ((long)Math.Max(1, students.Count) * (SummaryIdentityColumns + 3L * lessons.Count) > 1_000_000 || lessons.Count > 5_000)
            throw new ArgumentException("بيانات الكورس تتجاوز حجم ملف الملخص. اختر كورسًا أصغر.");
        var activity = await LoadSummaryActivityAsync(students.Select(student => student.Id).ToArray(), lessons, ct);
        var content = BuildCourseSummary(new(course, students, grants, activity, lessons));
        _db.AuditLogs.Add(new AuditLog { Action = "StudentCourseSummaryExported", EntityType = "TeacherProfile",
            EntityId = teacherId, PerformedByUserId = actorUserId,
            NewValues = JsonSerializer.Serialize(new { filter.Stage, filter.PackageId, StudentCount = students.Count, LessonCount = lessons.Count }) });
        await _db.SaveChangesAsync(ct);
        return new(content, ContentType, $"course-summary-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    private static List<SummaryLesson> SummaryLessons(Package course) => course.Terms
        .Where(term => term.ArchiveMode != ContentArchiveMode.HiddenFromEveryone).OrderBy(term => term.Order).ThenBy(term => term.Id)
        .SelectMany(term => term.Sections.Where(section => section.ArchiveMode != ContentArchiveMode.HiddenFromEveryone)
            .OrderBy(section => section.Order).ThenBy(section => section.Id)
            .SelectMany(section => section.Lessons.Where(lesson => lesson.ArchiveMode != ContentArchiveMode.HiddenFromEveryone)
                .OrderBy(lesson => lesson.Order).ThenBy(lesson => lesson.Id)
                .Select(lesson => new SummaryLesson(term, section, lesson,
                    lesson.Videos.Where(video => video.IsActive && video.ArchiveMode != ContentArchiveMode.HiddenFromEveryone)
                        .OrderBy(video => video.Order).ThenBy(video => video.Id).ToArray())))).ToList();

    private static byte[] BuildCourseSummary(SummaryDataset summary)
    {
        var (course, students, grants, activity, lessons) = summary;
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ملخص");
        sheet.RightToLeft = true; sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial"; sheet.Style.Font.FontSize = 11;
        WriteSummaryHeaders(sheet, course.Name, lessons);
        var grantsByStudent = grants.ToLookup(grant => grant.UserId);
        for (var index = 0; index < students.Count; index++)
        {
            var student = students[index]; var row = index + 5;
            object?[] identity = [student.Name, student.StudentCode, student.Phone, student.ParentPhone, EducationStageLabel(student.Stage), StudentFollowUpExportService.GradeLabel(student.Grade.ToString())];
            for (var column = 0; column < identity.Length; column++) sheet.Cell(row, column + 1).Value = XLCellValue.FromObject(identity[column] ?? "غير مسجل");
            WriteSummaryLessons(sheet.Row(row), new SummaryStudent(student.Id, grantsByStudent[student.Id].ToArray()), lessons, activity);
            sheet.Row(row).Height = Math.Min(300, Math.Max(42, 20 * sheet.Row(row).CellsUsed().Max(cell =>
                cell.GetString().Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / 22d))))));
        }
        var lastColumn = SummaryIdentityColumns + 3 * lessons.Count;
        sheet.SheetView.FreezeRows(4); sheet.SheetView.FreezeColumns(SummaryIdentityColumns);
        sheet.Range(4, 1, Math.Max(4, students.Count + 4), lastColumn).SetAutoFilter();
        double[] identityWidths = [30, 18, 20, 20, 18, 24];
        for (var index = 0; index < identityWidths.Length; index++) sheet.Column(index + 1).Width = identityWidths[index];
        if (lessons.Count > 0) sheet.Columns(SummaryIdentityColumns + 1, lastColumn).Width = 28;
        sheet.RangeUsed()!.Style.Alignment.WrapText = true;
        sheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(1).Height = 28; sheet.Row(2).Height = 44;
        sheet.Row(3).Height = 44; sheet.Row(4).Height = 32;
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    private static void WriteSummaryHeaders(IXLWorksheet sheet, string courseName, List<SummaryLesson> lessons)
    {
        var lastColumn = SummaryIdentityColumns + 3 * lessons.Count;
        sheet.Range(1, 1, 1, lastColumn).Merge().Value = $"ملخص الطلاب: {courseName}";
        StyleHeader(sheet.Range(1, 1, 1, lastColumn), "#0A1D3D");
        sheet.Range(2, 1, 2, lastColumn).Merge().Value = $"تصدير القاهرة: {CairoTime.ToLocal(DateTime.UtcNow):yyyy-MM-dd HH:mm}. شاهد = بدأ مشاهدة فيديو متاح له. الدرجات لآخر محاولة، والدرجة النهائية فقط بعد التصحيح.";
        string[] identity = ["اسم الطالب", "كود الطالب", "الموبايل", "ولي الأمر", "المرحلة", "الصف"];
        sheet.Range(3, 1, 3, SummaryIdentityColumns).Merge().Value = "بيانات الطالب";
        StyleHeader(sheet.Range(3, 1, 3, SummaryIdentityColumns), "#0A1D3D");
        for (var index = 0; index < identity.Length; index++)
        {
            sheet.Cell(4, index + 1).Value = identity[index];
            StyleHeader(sheet.Range(4, index + 1, 4, index + 1), "#0A1D3D");
        }
        for (var index = 0; index < lessons.Count; index++)
        {
            var column = SummaryIdentityColumns + index * 3 + 1; var lesson = lessons[index];
            sheet.Range(3, column, 3, column + 2).Merge().Value = $"{lesson.Term.Title} / {lesson.Section.Title} / {lesson.Lesson.Title}";
            StyleHeader(sheet.Range(3, column, 3, column + 2), "#0A1D3D");
            sheet.Cell(4, column).Value = "المشاهدة"; sheet.Cell(4, column + 1).Value = "درجة الامتحان"; sheet.Cell(4, column + 2).Value = "درجة الواجب";
            StyleHeader(sheet.Range(4, column, 4, column + 2), "#EEF1F4", XLColor.FromHtml("#0A1D3D"));
        }
    }

    private static void WriteSummaryLessons(IXLRow row, SummaryStudent student, List<SummaryLesson> lessons, SummaryActivity activity)
    {
        for (var index = 0; index < lessons.Count; index++)
        {
            var lesson = lessons[index]; var column = SummaryIdentityColumns + index * 3 + 1;
            var accessible = lesson.Videos.Where(video => student.Grants.Any(grant => CoversColumn(grant,
                new(lesson.Term.PackageId, "", "", "", "", LedgerColumnKind.VideoWatchStatus, lesson.Term.Id, lesson.Section.Id, lesson.Lesson.Id, video.Id, VideoTypeId: video.VideoTypeId)))).ToArray();
            var watched = accessible.Any(video => activity.StartedVideos.Contains((student.Id, video.Id)));
            if (accessible.Length == 0) SetStatus(row.Cell(column), "لا توجد فيديوهات متاحة ضمن اشتراكه", "#EEF1F4", "#2E3A47");
            else SetBooleanStatus(row.Cell(column), watched, "شاهد", "لم يشاهد");
            var examIds = lesson.Videos.Where(video => video.ExamId.HasValue).Select(video => video.ExamId!.Value)
                .Concat(lesson.Lesson.ExamId is { } examId ? [examId] : []).Distinct().ToArray();
            row.Cell(column + 1).Value = SummaryGrades(student.Id, examIds, activity.Exams, "لا يوجد امتحان");
            var homeworkIds = activity.Homeworks.Where(homework => homework.LessonId == lesson.Lesson.Id).Select(homework => homework.Id).ToArray();
            row.Cell(column + 2).Value = SummaryGrades(student.Id, homeworkIds, activity.HomeworkGrades, "لا يوجد واجب");
        }
    }

    private static string SummaryGrades(Guid studentId, Guid[] assessmentIds, SummaryGradeBook book, string none)
    {
        if (assessmentIds.Length == 0) return none;
        return string.Join("\n", assessmentIds.Select(id =>
        {
            var title = book.Titles.GetValueOrDefault(id, "تقييم");
            var grade = book.Grades.GetValueOrDefault((studentId, id), book.MissingGrade);
            return assessmentIds.Length == 1 ? grade : $"{title}: {grade}";
        }));
    }

    private sealed record SummaryLesson(Term Term, ContentSection Section, Lesson Lesson, LessonVideo[] Videos);
    private sealed record SummaryStudent(Guid Id, GrantRow[] Grants);
    private sealed record SummaryDataset(Package Course, List<StudentRow> Students, List<GrantRow> Grants, SummaryActivity Activity, List<SummaryLesson> Lessons);
    private sealed record SummaryGradeBook(string MissingGrade, Dictionary<Guid, string> Titles, Dictionary<(Guid StudentId, Guid AssessmentId), string> Grades);
    private sealed record SummaryActivity(HashSet<(Guid StudentId, Guid VideoId)> StartedVideos,
        SummaryGradeBook Exams, List<HomeworkRow> Homeworks, SummaryGradeBook HomeworkGrades);
}
