using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using NaderGorge.Application.Features.Reporting;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class StudentLedgerExportService : IStudentLedgerExportService
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly IAppDbContext _db;

    public StudentLedgerExportService(IAppDbContext db) => _db = db;

    public async Task<ReportExportDto> ExportForTeacherAsync(Guid actorUserId, CancellationToken ct)
    {
        var teacherId = await _db.TeacherProfiles.AsNoTracking()
            .Where(profile => profile.UserId == actorUserId)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(ct);

        teacherId ??= await _db.TeacherStaffMembers.AsNoTracking()
            .Where(member => member.UserId == actorUserId && member.IsActive && member.User.IsActive)
            .Select(member => (Guid?)member.TeacherId)
            .FirstOrDefaultAsync(ct);

        if (!teacherId.HasValue)
            throw new UnauthorizedAccessException("لا يوجد نطاق مدرس متاح لهذا الحساب.");

        return await ExportAsync(teacherId.Value, actorUserId, ct);
    }

    public async Task<ReportExportDto> ExportAsync(Guid teacherId, Guid actorUserId, CancellationToken ct)
    {
        var teacher = await _db.TeacherProfiles.AsNoTracking()
            .Where(profile => profile.Id == teacherId)
            .Select(profile => new { profile.Id, profile.User.FullName })
            .SingleOrDefaultAsync(ct) ?? throw new ArgumentException("المدرس غير موجود.");
        var packages = await LoadPackagesAsync(teacherId, ct);
        var packageIds = packages.Select(package => package.Id).ToHashSet();
        var grants = await LoadGrantsAsync(teacherId, ct);
        var studentIds = await LoadStudentIdsAsync(teacherId, grants, ct);
        var students = await LoadStudentsAsync(studentIds, ct);
        var activity = await LoadActivityAsync(studentIds, packageIds, ct);
        var bytes = BuildWorkbook(teacher.FullName, packages, students, grants, activity);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "StudentLedgerExported",
            EntityType = "TeacherProfile",
            EntityId = teacherId,
            PerformedByUserId = actorUserId,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { TeacherId = teacherId, StudentCount = students.Count, PackageCount = packages.Count })
        });
        await _db.SaveChangesAsync(ct);
        return new ReportExportDto(bytes, ContentType, $"student-ledger-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    private async Task<List<Package>> LoadPackagesAsync(Guid teacherId, CancellationToken ct) =>
        await _db.Packages.AsNoTracking().Where(package => package.TeacherId == teacherId)
            .Include(package => package.Terms).ThenInclude(term => term.Sections).ThenInclude(section => section.Lessons).ThenInclude(lesson => lesson.Videos)
            .OrderBy(package => package.Name).ToListAsync(ct);

    private async Task<List<GrantRow>> LoadGrantsAsync(Guid teacherId, CancellationToken ct) =>
        await _db.StudentAccessGrants.AsNoTracking().Where(grant =>
            (grant.PackageId.HasValue && _db.Packages.Any(package => package.Id == grant.PackageId && package.TeacherId == teacherId)) ||
            (grant.TermId.HasValue && _db.Terms.Any(term => term.Id == grant.TermId && term.Package.TeacherId == teacherId)) ||
            (grant.ContentSectionId.HasValue && _db.ContentSections.Any(section => section.Id == grant.ContentSectionId && section.Term.Package.TeacherId == teacherId)) ||
            (grant.LessonId.HasValue && _db.Lessons.Any(lesson => lesson.Id == grant.LessonId && lesson.ContentSection.Term.Package.TeacherId == teacherId)) ||
            (grant.LessonVideoId.HasValue && _db.LessonVideos.Any(video => video.Id == grant.LessonVideoId && video.Lesson.ContentSection.Term.Package.TeacherId == teacherId)) ||
            (grant.ExamId.HasValue && _db.Exams.Any(exam => exam.Id == grant.ExamId && exam.CreatedByTeacherId == teacherId)))
            .Select(grant => new GrantRow(grant.UserId, grant.PackageId, grant.TermId, grant.ContentSectionId, grant.LessonId, grant.LessonVideoId,
                grant.ExamId, grant.IsActive, grant.CancelledAt, grant.ExpiresAt)).ToListAsync(ct);

    private async Task<List<StudentRow>> LoadStudentsAsync(Guid[] studentIds, CancellationToken ct) =>
        await _db.Users.AsNoTracking().Where(user => studentIds.Contains(user.Id))
            .OrderBy(user => user.FullName)
            .Select(user => new StudentRow(user.Id, user.FullName, user.PhoneNumber,
                user.StudentProfile != null ? user.StudentProfile.ParentPhone : null,
                user.StudentProfile != null ? user.StudentProfile.StudentCode : null))
            .ToListAsync(ct);

    private async Task<Guid[]> LoadStudentIdsAsync(Guid teacherId, IEnumerable<GrantRow> grants, CancellationToken ct)
    {
        var ids = grants.Select(grant => grant.UserId).ToHashSet();
        var financialStudents = await _db.TeacherFinancialAllocations.AsNoTracking()
            .Where(allocation => allocation.TeacherId == teacherId && allocation.TeacherFinancialEvent.StudentId.HasValue)
            .Select(allocation => allocation.TeacherFinancialEvent.StudentId!.Value).Distinct().ToListAsync(ct);
        ids.UnionWith(financialStudents);
        return ids.ToArray();
    }

    private async Task<ActivityData> LoadActivityAsync(Guid[] studentIds, HashSet<Guid> packageIds, CancellationToken ct)
    {
        var watches = await LoadWatchesAsync(studentIds, packageIds, ct);
        var attempts = await LoadAttemptsAsync(studentIds, packageIds, ct);
        var homeworks = await LoadHomeworksAsync(packageIds, ct);
        var submissions = await LoadSubmissionsAsync(studentIds, homeworks, ct);
        return new ActivityData(watches, attempts, homeworks, submissions);
    }

    private Task<List<WatchRow>> LoadWatchesAsync(Guid[] studentIds, HashSet<Guid> packageIds, CancellationToken ct) =>
        _db.VideoWatchEvents.AsNoTracking()
            .Where(watch => studentIds.Contains(watch.UserId) && packageIds.Contains(watch.LessonVideo.Lesson.ContentSection.Term.PackageId))
            .Select(watch => new WatchRow(watch.UserId, watch.LessonVideoId, watch.LessonVideo.LessonId,
                watch.ActualWatchedSeconds, watch.WatchCount, watch.UpdatedAt ?? watch.CreatedAt,
                watch.LastPlaybackRate, watch.PlaybackRateBreakdownJson)).ToListAsync(ct);

    private async Task<List<AttemptRow>> LoadAttemptsAsync(Guid[] studentIds, HashSet<Guid> packageIds, CancellationToken ct)
    {
        var examIds = await _db.Exams.AsNoTracking().Where(exam => exam.LessonVideoId.HasValue && packageIds.Contains(exam.LessonVideo!.Lesson.ContentSection.Term.PackageId) ||
                !exam.LessonVideoId.HasValue && _db.Lessons.Any(lesson => lesson.ExamId == exam.Id && packageIds.Contains(lesson.ContentSection.Term.PackageId)))
            .Select(exam => exam.Id).ToListAsync(ct);
        return await _db.StudentExamAttempts.AsNoTracking()
            .Where(attempt => studentIds.Contains(attempt.UserId) && examIds.Contains(attempt.ExamId))
            .Select(attempt => new AttemptRow(attempt.UserId, attempt.ExamId, attempt.ScoreAchieved, attempt.IsPassed, attempt.CreatedAt)).ToListAsync(ct);
    }

    private Task<List<HomeworkRow>> LoadHomeworksAsync(HashSet<Guid> packageIds, CancellationToken ct) =>
        _db.Homeworks.AsNoTracking()
            .Where(homework => _db.Lessons.Any(lesson => lesson.Id == homework.LessonId && packageIds.Contains(lesson.ContentSection.Term.PackageId)))
            .Select(homework => new HomeworkRow(homework.Id, homework.LessonId, homework.Title)).ToListAsync(ct);

    private Task<List<SubmissionRow>> LoadSubmissionsAsync(Guid[] studentIds, IEnumerable<HomeworkRow> homeworks, CancellationToken ct)
    {
        var homeworkIds = homeworks.Select(homework => homework.Id).ToArray();
        return _db.HomeworkSubmissions.AsNoTracking()
            .Where(submission => studentIds.Contains(submission.StudentId) && homeworkIds.Contains(submission.HomeworkId))
            .Select(submission => new SubmissionRow(submission.StudentId, submission.HomeworkId, submission.Status, submission.OverallScore, submission.SubmittedAt)).ToListAsync(ct);
    }

    private static byte[] BuildWorkbook(string teacherName, List<Package> packages, List<StudentRow> students, List<GrantRow> grants, ActivityData activity)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("سجل الطلاب");
        sheet.RightToLeft = true;
        sheet.ShowGridLines = false;
        var columns = BuildColumns(packages, activity.Homeworks);
        WriteTitle(sheet, teacherName, columns.Count + 4);
        WriteHeaders(sheet, columns);
        StyleSheet(sheet, columns, students.Count + 6);
        WriteStudents(sheet, packages, columns, students, grants, activity);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static List<LedgerColumn> BuildColumns(IEnumerable<Package> packages, IReadOnlyList<HomeworkRow> homeworks)
    {
        var columns = new List<LedgerColumn>();
        foreach (var package in packages)
        {
            columns.Add(LedgerColumn.PackageStatus(package));
            foreach (var term in package.Terms.OrderBy(term => term.Order))
            foreach (var section in term.Sections.OrderBy(section => section.Order))
            foreach (var lesson in section.Lessons.OrderBy(lesson => lesson.Order))
            {
                columns.AddRange(LedgerColumn.Lesson(package, term, section, lesson, homeworks.FirstOrDefault(homework => homework.LessonId == lesson.Id)));
                foreach (var video in lesson.Videos.OrderBy(video => video.Order))
                    columns.AddRange(LedgerColumn.Video(package, term, section, lesson, video));
            }
        }
        return columns;
    }

    private static void WriteTitle(IXLWorksheet sheet, string teacherName, int lastColumn)
    {
        sheet.Range(1, 1, 1, lastColumn).Merge().Value = $"سجل حياة طلاب المدرس: {teacherName}";
        sheet.Range(1, 1, 1, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#0A1D3D");
        sheet.Range(1, 1, 1, lastColumn).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, lastColumn).Style.Font.FontSize = 16;
        sheet.Range(1, 1, 1, lastColumn).Style.Font.FontColor = XLColor.White;
        sheet.Range(2, 1, 2, lastColumn).Merge().Value = $"تاريخ التصدير: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        sheet.Range(2, 1, 2, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF1F4");
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<LedgerColumn> columns)
    {
        var baseHeaders = new[] { "اسم الطالب", "رقم الهاتف", "هاتف ولي الأمر", "كود الطالب" };
        for (var index = 0; index < baseHeaders.Length; index++)
        {
            sheet.Range(3, index + 1, 6, index + 1).Merge().Value = baseHeaders[index];
            StyleHeader(sheet.Range(3, index + 1, 6, index + 1), "#0A1D3D");
        }
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var excelColumn = index + 5;
            sheet.Cell(3, excelColumn).Value = column.PackageName;
            sheet.Cell(4, excelColumn).Value = column.TermTitle;
            sheet.Cell(5, excelColumn).Value = column.SectionTitle;
            sheet.Cell(6, excelColumn).Value = column.ItemTitle;
        }
        MergeEqualHeaders(sheet, columns, 3, column => column.PackageName, "#0A1D3D");
        MergeEqualHeaders(sheet, columns, 4, column => $"{column.PackageId}:{column.TermTitle}", "#0E8F8F");
        MergeEqualHeaders(sheet, columns, 5, column => $"{column.PackageId}:{column.TermTitle}:{column.SectionTitle}", "#2E3A47");
        for (var index = 0; index < columns.Count; index++)
            StyleHeader(sheet.Range(6, index + 5, 6, index + 5), HeaderFill(columns[index].Kind), XLColor.FromHtml("#0A1D3D"));
    }

    private static void MergeEqualHeaders(IXLWorksheet sheet, IReadOnlyList<LedgerColumn> columns, int row, Func<LedgerColumn, string> key, string color)
    {
        var start = 0;
        while (start < columns.Count)
        {
            var end = start;
            while (end + 1 < columns.Count && key(columns[end + 1]) == key(columns[start])) end++;
            var range = sheet.Range(row, start + 5, row, end + 5);
            if (end > start) range.Merge();
            StyleHeader(range, color);
            start = end + 1;
        }
    }

    private static void WriteStudents(IXLWorksheet sheet, IReadOnlyList<Package> packages, IReadOnlyList<LedgerColumn> columns, IReadOnlyList<StudentRow> students, IReadOnlyList<GrantRow> grants, ActivityData activity)
    {
        var packageMap = BuildPackageMap(packages);
        for (var studentIndex = 0; studentIndex < students.Count; studentIndex++)
        {
            var row = studentIndex + 7;
            var student = students[studentIndex];
            sheet.Cell(row, 1).Value = student.Name;
            sheet.Cell(row, 2).Value = student.Phone;
            sheet.Cell(row, 3).Value = student.ParentPhone ?? string.Empty;
            sheet.Cell(row, 4).Value = student.StudentCode ?? string.Empty;
            var studentGrants = grants.Where(grant => grant.UserId == student.Id).ToList();
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var column = columns[columnIndex];
                var cell = sheet.Cell(row, columnIndex + 5);
                var access = ResolveAccess(column, studentGrants, packageMap);
                if (column.Kind == LedgerColumnKind.PackageStatus) WritePurchaseCell(cell, access);
                else if (!access.Active) WriteUnavailableCell(cell);
                else if (column.VideoId.HasValue) WriteVideoMetricCell(cell, student.Id, column, activity);
                else WriteLessonMetricCell(cell, student.Id, column, activity);
            }
        }
    }

    private static Dictionary<Guid, Guid> BuildPackageMap(IEnumerable<Package> packages)
    {
        var map = new Dictionary<Guid, Guid>();
        foreach (var package in packages)
        {
            map[package.Id] = package.Id;
            AddPackageContentToMap(map, package);
        }
        return map;
    }

    private static void AddPackageContentToMap(IDictionary<Guid, Guid> map, Package package)
    {
        foreach (var term in package.Terms)
        {
            map[term.Id] = package.Id;
            foreach (var section in term.Sections)
            {
                map[section.Id] = package.Id;
                foreach (var lesson in section.Lessons)
                {
                    map[lesson.Id] = package.Id;
                    if (lesson.ExamId.HasValue) map[lesson.ExamId.Value] = package.Id;
                    foreach (var video in lesson.Videos)
                    {
                        map[video.Id] = package.Id;
                        if (video.ExamId.HasValue) map[video.ExamId.Value] = package.Id;
                    }
                }
            }
        }
    }

    private static AccessState ResolveAccess(LedgerColumn column, IEnumerable<GrantRow> grants, IReadOnlyDictionary<Guid, Guid> packageMap)
    {
        var samePackage = grants.Where(grant => grant.TargetIds().Any(id => packageMap.GetValueOrDefault(id) == column.PackageId)).ToList();
        var matching = column.Kind == LedgerColumnKind.PackageStatus
            ? samePackage
            : samePackage.Where(grant => grant.PackageId == column.PackageId || grant.TermId == column.TermId || grant.SectionId == column.SectionId || grant.LessonId == column.LessonId || grant.VideoId == column.VideoId || grant.ExamId == column.ExamId).ToList();
        var now = DateTime.UtcNow;
        var active = matching.Any(grant => grant.IsActive && !grant.CancelledAt.HasValue && (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now));
        var direct = matching.Any(grant => grant.PackageId == column.PackageId && grant.IsActive && !grant.CancelledAt.HasValue && (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now));
        return new AccessState(samePackage.Count > 0, active, direct);
    }

    private static void WritePurchaseCell(IXLCell cell, AccessState access)
    {
        if (access.Direct) SetStatus(cell, "مشترى", "#DCFCE7", "#166534");
        else if (access.Active) SetStatus(cell, "وصول جزئي", "#DBEAFE", "#1E40AF");
        else if (access.HasAny) SetStatus(cell, "منتهي", "#FEF3C7", "#92400E");
        else SetStatus(cell, "لم يشترِ", "#FEE2E2", "#991B1B");
    }

    private static void WriteUnavailableCell(IXLCell cell) => SetStatus(cell, "لم يشترِ", "#FEE2E2", "#991B1B");

    private static void WriteLessonMetricCell(IXLCell cell, Guid studentId, LedgerColumn column, ActivityData activity)
    {
        var watched = activity.Watches.Where(watch => watch.UserId == studentId && watch.LessonId == column.LessonId).ToList();
        var exam = LatestAttempt(activity.Attempts, studentId, column.ExamId);
        var homework = LatestSubmission(activity.Submissions, studentId, column.HomeworkId);
        switch (column.Kind)
        {
            case LedgerColumnKind.Attendance:
                SetBooleanStatus(cell, watched.Count > 0, "حاضر", "غائب");
                break;
            case LedgerColumnKind.ExamAttemptStatus:
            case LedgerColumnKind.ExamScore:
            case LedgerColumnKind.ExamResult:
                WriteExamMetric(cell, column.Kind, exam);
                break;
            case LedgerColumnKind.HomeworkSubmissionStatus:
            case LedgerColumnKind.HomeworkScore:
            case LedgerColumnKind.HomeworkSubmittedAt:
                WriteHomeworkMetric(cell, column.Kind, homework);
                break;
        }
    }

    private static void WriteVideoMetricCell(IXLCell cell, Guid studentId, LedgerColumn column, ActivityData activity)
    {
        var watch = activity.Watches.Where(item => item.UserId == studentId && item.VideoId == column.VideoId).OrderByDescending(item => item.LastWatchedAt).FirstOrDefault();
        var exam = LatestAttempt(activity.Attempts, studentId, column.ExamId);
        switch (column.Kind)
        {
            case LedgerColumnKind.VideoWatchStatus:
                SetBooleanStatus(cell, watch != null, "شاهد", "لم يشاهد");
                break;
            case LedgerColumnKind.WatchedMinutes:
                SetMetric(cell, watch == null ? null : Math.Round(watch.Seconds / 60, 2));
                break;
            case LedgerColumnKind.WatchCount:
                SetMetric(cell, watch?.WatchCount);
                break;
            case LedgerColumnKind.LastWatchedAt:
                SetMetric(cell, watch?.LastWatchedAt);
                break;
            case LedgerColumnKind.PlaybackRates:
                SetMetric(cell, PlaybackRatesText(watch));
                break;
            case LedgerColumnKind.ExamAttemptStatus:
            case LedgerColumnKind.ExamScore:
            case LedgerColumnKind.ExamResult:
                WriteExamMetric(cell, column.Kind, exam);
                break;
        }
    }

    private static string? PlaybackRatesText(WatchRow? watch)
    {
        if (watch == null) return null;
        try
        {
            var rates = (JsonSerializer.Deserialize<Dictionary<string, decimal>>(watch.PlaybackRateBreakdownJson) ?? [])
                .Where(entry => entry.Value > 0 && decimal.TryParse(entry.Key, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                .Select(entry => decimal.Parse(entry.Key, NumberStyles.Number, CultureInfo.InvariantCulture))
                .Distinct().OrderBy(rate => rate).ToArray();
            return rates.Length == 0
                ? $"{watch.LastPlaybackRate:0.##}×"
                : string.Join("، ", rates.Select(rate => $"{rate:0.##}×"));
        }
        catch (JsonException)
        {
            return $"{watch.LastPlaybackRate:0.##}×";
        }
    }

    private static void WriteExamMetric(IXLCell cell, LedgerColumnKind kind, AttemptRow? attempt)
    {
        if (kind == LedgerColumnKind.ExamAttemptStatus) SetBooleanStatus(cell, attempt != null, "دخل", "لم يدخل");
        else if (kind == LedgerColumnKind.ExamScore) SetMetric(cell, attempt?.Score);
        else SetMetric(cell, attempt == null ? null : attempt.Passed ? "ناجح" : "لم ينجح", attempt?.Passed);
    }

    private static void WriteHomeworkMetric(IXLCell cell, LedgerColumnKind kind, SubmissionRow? submission)
    {
        if (kind == LedgerColumnKind.HomeworkSubmissionStatus) SetBooleanStatus(cell, submission != null, "سلّم", "لم يسلّم");
        else if (kind == LedgerColumnKind.HomeworkScore) SetMetric(cell, submission?.Score);
        else SetMetric(cell, submission?.SubmittedAt);
    }

    private static AttemptRow? LatestAttempt(IEnumerable<AttemptRow> attempts, Guid studentId, Guid? examId) =>
        !examId.HasValue ? null : attempts.Where(attempt => attempt.UserId == studentId && attempt.ExamId == examId).OrderByDescending(attempt => attempt.CreatedAt).FirstOrDefault();

    private static SubmissionRow? LatestSubmission(IEnumerable<SubmissionRow> submissions, Guid studentId, Guid? homeworkId) =>
        !homeworkId.HasValue ? null : submissions.Where(submission => submission.StudentId == studentId && submission.HomeworkId == homeworkId).OrderByDescending(submission => submission.SubmittedAt).FirstOrDefault();

    private static void SetBooleanStatus(IXLCell cell, bool positive, string positiveText, string negativeText) =>
        SetStatus(cell, positive ? positiveText : negativeText, positive ? "#DCFCE7" : "#FEE2E2", positive ? "#166534" : "#991B1B");

    private static void SetMetric(IXLCell cell, object? metric, bool? positive = null)
    {
        switch (metric)
        {
            case null:
                cell.Value = "—";
                break;
            case int count:
                cell.Value = count;
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case decimal number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0.##";
                break;
            case DateTime date:
                cell.Value = date;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                break;
            default:
                cell.Value = metric.ToString() ?? "—";
                break;
        }
        if (!positive.HasValue) return;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(positive.Value ? "#DCFCE7" : "#FEE2E2");
        cell.Style.Font.FontColor = XLColor.FromHtml(positive.Value ? "#166534" : "#991B1B");
    }

    private static void SetStatus(IXLCell cell, string statusText, string fill, string font)
    {
        cell.Value = statusText;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(fill);
        cell.Style.Font.FontColor = XLColor.FromHtml(font);
        cell.Style.Alignment.WrapText = true;
    }

    private static void StyleHeader(IXLRange range, string fill, XLColor? font = null)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(fill);
        range.Style.Font.SetBold();
        range.Style.Font.FontColor = font ?? XLColor.White;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
    }

    private static void StyleSheet(IXLWorksheet sheet, IReadOnlyList<LedgerColumn> columns, int lastRow)
    {
        var lastColumn = columns.Count + 4;
        sheet.SheetView.FreezeRows(6);
        sheet.SheetView.FreezeColumns(4);
        sheet.Range(3, 1, lastRow, lastColumn).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(3, 1, lastRow, lastColumn).Style.Border.InsideBorderColor = XLColor.FromHtml("#DCE1E6");
        sheet.Range(3, 1, lastRow, lastColumn).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Columns(1, 4).AdjustToContents(1, 40);
        if (lastColumn >= 5) sheet.Columns(5, lastColumn).Width = 24;
        sheet.Rows(3, 6).Height = 28;
        sheet.Row(1).Height = 32;
        StyleMetricColumns(sheet, columns, lastRow);
        StyleColumnGroups(sheet, columns, lastRow);
    }

    private static void StyleMetricColumns(IXLWorksheet sheet, IReadOnlyList<LedgerColumn> columns, int lastRow)
    {
        if (lastRow < 7) return;
        for (var index = 0; index < columns.Count; index++)
            sheet.Range(7, index + 5, lastRow, index + 5).Style.Fill.BackgroundColor = XLColor.FromHtml(BodyFill(columns[index].Kind));
    }

    private static void StyleColumnGroups(IXLWorksheet sheet, IReadOnlyList<LedgerColumn> columns, int lastRow)
    {
        var start = 0;
        while (start < columns.Count)
        {
            var end = start;
            while (end + 1 < columns.Count && columns[end + 1].PackageId == columns[start].PackageId) end++;
            SetVerticalBorders(sheet.Range(3, start + 5, lastRow, end + 5), XLBorderStyleValues.Medium, "#0A1D3D");
            start = end + 1;
        }

        for (var index = 0; index < columns.Count; index++)
        {
            var nextIsDifferentItem = index == columns.Count - 1 || ColumnItemKey(columns[index + 1]) != ColumnItemKey(columns[index]);
            if (!nextIsDifferentItem) continue;
            var boundary = sheet.Range(5, index + 5, lastRow, index + 5);
            boundary.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            boundary.Style.Border.LeftBorderColor = XLColor.FromHtml("#94A3B8");
        }
    }

    private static void SetVerticalBorders(IXLRange range, XLBorderStyleValues style, string color)
    {
        range.Style.Border.LeftBorder = style;
        range.Style.Border.RightBorder = style;
        range.Style.Border.LeftBorderColor = XLColor.FromHtml(color);
        range.Style.Border.RightBorderColor = XLColor.FromHtml(color);
    }

    private static string ColumnItemKey(LedgerColumn column) =>
        column.VideoId?.ToString() ?? column.LessonId?.ToString() ?? column.PackageId.ToString();

    private static string HeaderFill(LedgerColumnKind kind) => kind switch
    {
        LedgerColumnKind.PackageStatus => "#FDE68A",
        LedgerColumnKind.Attendance => "#BBF7D0",
        LedgerColumnKind.ExamAttemptStatus or LedgerColumnKind.ExamScore or LedgerColumnKind.ExamResult => "#BFDBFE",
        LedgerColumnKind.HomeworkSubmissionStatus or LedgerColumnKind.HomeworkScore or LedgerColumnKind.HomeworkSubmittedAt => "#FED7AA",
        _ => "#99F6E4"
    };

    private static string BodyFill(LedgerColumnKind kind) => kind switch
    {
        LedgerColumnKind.PackageStatus => "#FFFBEB",
        LedgerColumnKind.Attendance => "#F0FDF4",
        LedgerColumnKind.ExamAttemptStatus or LedgerColumnKind.ExamScore or LedgerColumnKind.ExamResult => "#EFF6FF",
        LedgerColumnKind.HomeworkSubmissionStatus or LedgerColumnKind.HomeworkScore or LedgerColumnKind.HomeworkSubmittedAt => "#FFF7ED",
        _ => "#F0FDFA"
    };

    private sealed record StudentRow(Guid Id, string Name, string Phone, string? ParentPhone, string? StudentCode);
    private sealed record GrantRow(Guid UserId, Guid? PackageId, Guid? TermId, Guid? SectionId, Guid? LessonId, Guid? VideoId, Guid? ExamId, bool IsActive, DateTime? CancelledAt, DateTime? ExpiresAt)
    {
        public IEnumerable<Guid> TargetIds() => new Guid?[] { PackageId, TermId, SectionId, LessonId, VideoId, ExamId }.OfType<Guid>();
    }
    private sealed record WatchRow(Guid UserId, Guid VideoId, Guid LessonId, decimal Seconds, int WatchCount, DateTime LastWatchedAt, decimal LastPlaybackRate, string PlaybackRateBreakdownJson);
    private sealed record AttemptRow(Guid UserId, Guid ExamId, decimal Score, bool Passed, DateTime CreatedAt);
    private sealed record HomeworkRow(Guid Id, Guid LessonId, string Title);
    private sealed record SubmissionRow(Guid StudentId, Guid HomeworkId, SubmissionStatus Status, decimal Score, DateTime? SubmittedAt);
    private sealed record ActivityData(List<WatchRow> Watches, List<AttemptRow> Attempts, List<HomeworkRow> Homeworks, List<SubmissionRow> Submissions);
    private sealed record AccessState(bool HasAny, bool Active, bool Direct);
    private enum LedgerColumnKind
    {
        PackageStatus,
        Attendance,
        ExamAttemptStatus,
        ExamScore,
        ExamResult,
        HomeworkSubmissionStatus,
        HomeworkScore,
        HomeworkSubmittedAt,
        VideoWatchStatus,
        WatchedMinutes,
        WatchCount,
        LastWatchedAt,
        PlaybackRates
    }
    private sealed record LedgerColumn(Guid PackageId, string PackageName, string TermTitle, string SectionTitle, string ItemTitle, LedgerColumnKind Kind, Guid? TermId = null, Guid? SectionId = null, Guid? LessonId = null, Guid? VideoId = null, Guid? ExamId = null, Guid? HomeworkId = null)
    {
        public static LedgerColumn PackageStatus(Package package) => new(package.Id, package.Name, "حالة الشراء", "حالة الشراء", "حالة الباقة", LedgerColumnKind.PackageStatus);
        public static IEnumerable<LedgerColumn> Lesson(Package package, Term term, ContentSection section, NaderGorge.Domain.Entities.Lesson lesson, HomeworkRow? homework)
        {
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{lesson.Title} - الحضور", LedgerColumnKind.Attendance, term.Id, section.Id, lesson.Id);
            if (lesson.ExamId.HasValue)
            {
                yield return new(package.Id, package.Name, term.Title, section.Title, $"{lesson.Title} - دخول الامتحان", LedgerColumnKind.ExamAttemptStatus, term.Id, section.Id, lesson.Id, ExamId: lesson.ExamId);
                yield return new(package.Id, package.Name, term.Title, section.Title, $"{lesson.Title} - درجة الامتحان", LedgerColumnKind.ExamScore, term.Id, section.Id, lesson.Id, ExamId: lesson.ExamId);
                yield return new(package.Id, package.Name, term.Title, section.Title, $"{lesson.Title} - نتيجة الامتحان", LedgerColumnKind.ExamResult, term.Id, section.Id, lesson.Id, ExamId: lesson.ExamId);
            }
            if (homework != null)
            {
                yield return new(package.Id, package.Name, term.Title, section.Title, $"{lesson.Title} - تسليم الواجب", LedgerColumnKind.HomeworkSubmissionStatus, term.Id, section.Id, lesson.Id, HomeworkId: homework.Id);
                yield return new(package.Id, package.Name, term.Title, section.Title, $"{lesson.Title} - درجة الواجب", LedgerColumnKind.HomeworkScore, term.Id, section.Id, lesson.Id, HomeworkId: homework.Id);
                yield return new(package.Id, package.Name, term.Title, section.Title, $"{lesson.Title} - تاريخ التسليم", LedgerColumnKind.HomeworkSubmittedAt, term.Id, section.Id, lesson.Id, HomeworkId: homework.Id);
            }
        }

        public static IEnumerable<LedgerColumn> Video(Package package, Term term, ContentSection section, NaderGorge.Domain.Entities.Lesson lesson, LessonVideo video)
        {
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - المشاهدة", LedgerColumnKind.VideoWatchStatus, term.Id, section.Id, lesson.Id, video.Id);
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - دقائق المشاهدة", LedgerColumnKind.WatchedMinutes, term.Id, section.Id, lesson.Id, video.Id);
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - عدد المشاهدات", LedgerColumnKind.WatchCount, term.Id, section.Id, lesson.Id, video.Id);
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - آخر مشاهدة", LedgerColumnKind.LastWatchedAt, term.Id, section.Id, lesson.Id, video.Id);
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - سرعات المشاهدة", LedgerColumnKind.PlaybackRates, term.Id, section.Id, lesson.Id, video.Id);
            if (!video.ExamId.HasValue) yield break;
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - دخول الامتحان", LedgerColumnKind.ExamAttemptStatus, term.Id, section.Id, lesson.Id, video.Id, video.ExamId);
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - درجة الامتحان", LedgerColumnKind.ExamScore, term.Id, section.Id, lesson.Id, video.Id, video.ExamId);
            yield return new(package.Id, package.Name, term.Title, section.Title, $"{video.Title} - نتيجة الامتحان", LedgerColumnKind.ExamResult, term.Id, section.Id, lesson.Id, video.Id, video.ExamId);
        }
    }
}
