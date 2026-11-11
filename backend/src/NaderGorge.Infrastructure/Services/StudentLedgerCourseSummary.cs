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
    private static readonly string[] SummaryIdentityHeaders = ["اسم الطالب", "كود الطالب", "الموبايل", "ولي الأمر", "المرحلة", "الصف",
        "المحافظة", "المنطقة", "العنوان", "المدرسة", "الشعبة", "موبايل إضافي", "ولي الأمر الإضافي", "الأم", "الجنسية"];
    private static int SummaryIdentityColumns => SummaryIdentityHeaders.Length;

    public async Task<StudentCourseSummaryReport> ReadCourseSummaryAsync(Guid teacherId,
        StudentCourseSummaryFilter filter, CancellationToken ct)
    {
        var summary = await LoadCourseSummaryAsync(teacherId, filter, ct);
        var columns = SummaryColumns(summary.Lessons);
        var grants = summary.Grants.ToLookup(grant => grant.UserId);
        var rows = summary.Students.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(student => SummaryPreviewRow(student, grants[student.Id].ToArray(), summary, columns)).ToArray();
        return new(columns, rows, summary.Students.Count, filter.Page, filter.PageSize);
    }

    public async Task<ReportExportDto> ExportCourseSummaryAsync(Guid teacherId, Guid actorUserId,
        StudentCourseSummaryFilter filter, CancellationToken ct)
    {
        var summary = await LoadCourseSummaryAsync(teacherId, filter, ct);
        var content = BuildCourseSummary(summary);
        _db.AuditLogs.Add(new AuditLog { Action = "StudentCourseSummaryExported", EntityType = "TeacherProfile",
            EntityId = teacherId, PerformedByUserId = actorUserId,
            NewValues = JsonSerializer.Serialize(new { filter.Stage, filter.PackageId, filter.Grade, StudentCount = summary.Students.Count, LessonCount = summary.Lessons.Count }) });
        await _db.SaveChangesAsync(ct);
        return new(content, ContentType, $"course-summary-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    private async Task<SummaryDataset> LoadCourseSummaryAsync(Guid teacherId, StudentCourseSummaryFilter filter, CancellationToken ct)
    {
        ValidateSummaryFilter(filter);
        var packages = await LoadPackagesAsync(teacherId, filter.PackageId, ct);
        if (filter.PackageId.HasValue && packages.Count == 0)
            throw new ArgumentException("الكورس غير موجود ضمن كورساتك.");
        var map = BuildPackageMap(packages);
        var grants = (await LoadGrantsAsync(teacherId, ct))
            .Where(grant => packages.Any(course => BelongsToPackage(grant, course.Id, map, packages))).ToList();
        var students = await LoadStudentsAsync(grants.Select(grant => grant.UserId).Distinct().ToArray(), new(filter.Stage), ct);
        students = students.Where(student => !filter.Grade.HasValue || student.Grade == filter.Grade).ToList();
        var lessons = packages.SelectMany(SummaryLessons).ToList();
        if ((long)Math.Max(1, students.Count) * (SummaryIdentityColumns + 3L * lessons.Count) > 1_000_000 || lessons.Count > 5_000)
            throw new ArgumentException("بيانات التقرير تتجاوز حجم الملخص. حدد مرحلة أو صفًا أو كورسًا قبل المحاولة.");
        var activity = await LoadSummaryActivityAsync(students.Select(student => student.Id).ToArray(), lessons, ct);
        return new(packages.Count == 1 ? packages[0].Name : "كل كورسات المدرس", students, grants, activity, lessons);
    }

    private static void ValidateSummaryFilter(StudentCourseSummaryFilter filter)
    {
        // Null includes every stage/course/grade; an explicit invalid selection must never broaden the scope.
        if ((filter.Stage.HasValue && !Enum.IsDefined(filter.Stage.Value)) || filter.PackageId == Guid.Empty
            || (filter.Grade.HasValue && !Enum.IsDefined(filter.Grade.Value)))
            throw new ArgumentException("اختر مرحلة أو صفًا أو كورسًا صحيحًا.");
        if (filter.Page is < 1 or > 1_000_000 || filter.PageSize is < 1 or > 100)
            throw new ArgumentException("رقم الصفحة أو حجمها غير صالح.");
    }

    private static string[] SummaryIdentityValues(StudentRow student) =>
        [student.Name, SummaryText(student.StudentCode), student.Phone, SummaryText(student.ParentPhone),
         EducationStageLabel(student.Stage), StudentFollowUpExportService.GradeLabel(student.Grade.ToString()),
         SummaryText(student.Governorate), SummaryText(student.District), SummaryText(student.Address), SummaryText(student.SchoolName),
         student.StudyTrack is { } track ? StudyTrackLabel(track) : "غير مسجل", SummaryText(student.SecondaryPhone), SummaryText(student.SecondaryParentPhone),
         SummaryText(student.MotherPhone), SummaryText(student.Nationality)];

    private static string SummaryText(string? text) => string.IsNullOrWhiteSpace(text) ? "غير مسجل" : text;

    private static List<ReportColumnDto> SummaryColumns(List<SummaryLesson> lessons)
    {
        var columns = SummaryIdentityHeaders.Select((label, index) => new ReportColumnDto($"identity{index}", label, "text")).ToList();
        foreach (var lesson in lessons)
            foreach (var label in new[] { "المشاهدة", "درجة الامتحان", "درجة الواجب" })
                columns.Add(new($"lesson{columns.Count - SummaryIdentityColumns}",
                    $"{lesson.Term.Package.Name} / {lesson.Term.Title} / {lesson.Section.Title} / {lesson.Lesson.Title} / {label}", "text"));
        return columns;
    }

    private static IReadOnlyDictionary<string, object?> SummaryPreviewRow(StudentRow student, GrantRow[] grants,
        SummaryDataset summary, List<ReportColumnDto> columns)
    {
        var cells = SummaryIdentityValues(student).Concat(summary.Lessons.SelectMany(lesson =>
            SummaryLessonValues(new(student.Id, grants), lesson, summary.Activity))).ToArray();
        var row = columns.Select((column, index) => (column.Key, Cell: (object?)cells[index]))
            .ToDictionary(cell => cell.Key, cell => cell.Cell);
        row["id"] = student.Id;
        return row;
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
        var (courseName, students, grants, activity, lessons) = summary;
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ملخص");
        sheet.RightToLeft = true; sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial"; sheet.Style.Font.FontSize = 11;
        WriteSummaryHeaders(sheet, courseName, lessons);
        var grantsByStudent = grants.ToLookup(grant => grant.UserId);
        for (var index = 0; index < students.Count; index++)
        {
            var student = students[index]; var row = index + 5;
            var identity = SummaryIdentityValues(student);
            for (var column = 0; column < identity.Length; column++) sheet.Cell(row, column + 1).Value = XLCellValue.FromObject(identity[column] ?? "غير مسجل");
            WriteSummaryLessons(sheet.Row(row), new SummaryStudent(student.Id, grantsByStudent[student.Id].ToArray()), lessons, activity);
            sheet.Row(row).Height = Math.Min(300, Math.Max(42, 20 * sheet.Row(row).CellsUsed().Max(cell =>
                cell.GetString().Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / 22d))))));
        }
        var lastColumn = SummaryIdentityColumns + 3 * lessons.Count;
        sheet.SheetView.FreezeRows(4); sheet.SheetView.FreezeColumns(SummaryIdentityColumns);
        sheet.Range(4, 1, Math.Max(4, students.Count + 4), lastColumn).SetAutoFilter();
        double[] identityWidths = [30, 18, 20, 20, 18, 24, 20, 20, 35, 28, 24, 20, 20, 20, 18];
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
        var identity = SummaryIdentityHeaders;
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
            sheet.Range(3, column, 3, column + 2).Merge().Value = $"{lesson.Term.Package.Name} / {lesson.Term.Title} / {lesson.Section.Title} / {lesson.Lesson.Title}";
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
            var cells = SummaryLessonValues(student, lesson, activity);
            if (cells[0] == "شاهد") SetBooleanStatus(row.Cell(column), true, "شاهد", "لم يشاهد");
            else if (cells[0] == "لم يشاهد") SetBooleanStatus(row.Cell(column), false, "شاهد", "لم يشاهد");
            else SetStatus(row.Cell(column), cells[0], "#EEF1F4", "#2E3A47");
            row.Cell(column + 1).Value = cells[1];
            row.Cell(column + 2).Value = cells[2];
        }
    }

    private static string[] SummaryLessonValues(SummaryStudent student, SummaryLesson lesson, SummaryActivity activity)
    {
        var accessible = lesson.Videos.Where(video => student.Grants.Any(grant => CoversColumn(grant,
            new(lesson.Term.PackageId, "", "", "", "", LedgerColumnKind.VideoWatchStatus,
                lesson.Term.Id, lesson.Section.Id, lesson.Lesson.Id, video.Id, VideoTypeId: video.VideoTypeId)))).ToArray();
        var watch = accessible.Length == 0 ? "لا توجد فيديوهات متاحة ضمن اشتراكه"
            : accessible.Any(video => activity.StartedVideos.Contains((student.Id, video.Id))) ? "شاهد" : "لم يشاهد";
        var examIds = lesson.Videos.Where(video => video.ExamId.HasValue).Select(video => video.ExamId!.Value)
            .Concat(lesson.Lesson.ExamId is { } examId ? [examId] : []).Distinct().ToArray();
        var homeworkIds = activity.Homeworks.Where(homework => homework.LessonId == lesson.Lesson.Id).Select(homework => homework.Id).ToArray();
        return [watch, SummaryGrades(student.Id, examIds, activity.Exams, "لا يوجد امتحان"),
            SummaryGrades(student.Id, homeworkIds, activity.HomeworkGrades, "لا يوجد واجب")];
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
    private sealed record SummaryDataset(string CourseName, List<StudentRow> Students, List<GrantRow> Grants, SummaryActivity Activity, List<SummaryLesson> Lessons);
    private sealed record SummaryGradeBook(string MissingGrade, Dictionary<Guid, string> Titles, Dictionary<(Guid StudentId, Guid AssessmentId), string> Grades);
    private sealed record SummaryActivity(HashSet<(Guid StudentId, Guid VideoId)> StartedVideos,
        SummaryGradeBook Exams, List<HomeworkRow> Homeworks, SummaryGradeBook HomeworkGrades);
}
