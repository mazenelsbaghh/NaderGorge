using System.Text.Json;
using ClosedXML.Excel;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.StudentFollowUp;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class StudentFollowUpExportService(IAppDbContext db) : IStudentFollowUpExporter
{
    public async Task<byte[]> ExportAsync(StudentFollowUpReport report, StudentFollowUpFilter filter, Guid actorId, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("متابعة الطلاب");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "متابعة الطلاب";
        sheet.Cell(2, 1).Value = $"وقت الإنشاء (القاهرة): {CairoTime.ToLocal(report.GeneratedAtUtc):yyyy-MM-dd HH:mm}";
        sheet.Cell(3, 1).Value = $"الحالة: {StateLabel(filter.State)}، مدة التأخير: {filter.MinimumDays} يوم أو أكثر، عدد الطلاب: {report.TotalStudents}";
        sheet.Cell(4, 1).Value = "التأخير من الأحدث بين إتاحة الفيديو والحصول على صلاحية المشاهدة. تاريخ الحصة ليس بداية التأخير.";
        string[] headers = ["اسم الطالب", "موبايل الطالب", "رقم ولي الأمر", "الصف", "الكورس", "الحصة", "المشاهدة", "أجزاء مكتملة", "عدد الأجزاء", "نسبة التقدم", "بداية الإتاحة للطالب (القاهرة)", "أيام الانتظار", "مصدر تاريخ الإتاحة", "آخر مشاهدة (القاهرة)"];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(6, column + 1).Value = headers[column];
        for (var index = 0; index < report.Rows.Count; index++) WriteRow(sheet, index + 7, report.Rows[index]);
        sheet.Range(6, 1, Math.Max(6, report.Rows.Count + 6), headers.Length).SetAutoFilter();
        sheet.Row(6).Style.Font.Bold = true;
        sheet.Row(6).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF1F4");
        sheet.SheetView.FreezeRows(6);
        sheet.Columns().AdjustToContents(12, 45);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "StudentFollowUpExported", EntityType = "StudentFollowUp", PerformedByUserId = actorId,
            NewValues = JsonSerializer.Serialize(new { filter.TeacherId, filter.PackageId, filter.LessonIds,
                filter.Stage, filter.Grade, filter.State, filter.MinimumDays, report.TotalRows, report.TotalStudents })
        });
        await db.SaveChangesAsync(ct);
        return stream.ToArray();
    }

    private static void WriteRow(IXLWorksheet sheet, int row, StudentFollowUpRow data)
    {
        object?[] values = [data.StudentName, data.Phone, data.ParentPhone, GradeLabel(data.Grade), data.PackageName,
            data.LessonTitle, StateLabel(data.State), data.CompletedVideos, data.TotalVideos,
            data.ProgressPercentage is { } percent ? $"{percent}%" : "المدة غير مسجلة",
            DateLabel(data.AvailableSinceUtc), data.DaysWaiting, SourceLabel(data.AvailabilitySource), DateLabel(data.LastWatchedAtUtc)];
        for (var column = 0; column < values.Length; column++) sheet.Cell(row, column + 1).Value = XLCellValue.FromObject(values[column] ?? "غير مسجل");
    }

    private static string DateLabel(DateTime? date) => date.HasValue ? CairoTime.ToLocal(date.Value).ToString("yyyy-MM-dd HH:mm") : "غير مسجل";
    private static string SourceLabel(string source) => source switch
    { "VideoOpened" => "وقت فتح الفيديو", "RecordedPlayback" => "أقدم مشاهدة طالب موثقة", _ => "غير مسجل" };
    private static string StateLabel(string state) => state switch
    { "Completed" => "مكتمل", "Started" => "بدأ المشاهدة", "NotStarted" => "لم يبدأ في السجل", "Incomplete" => "لم يكتمل في السجل", _ => "الكل" };

    private static string GradeLabel(string grade) => grade switch
    {
        "FirstSecondary" => "الأول الثانوي", "SecondSecondary" => "الثاني الثانوي", "SecondaryGrade3" => "الثالث الثانوي",
        "FirstBaccalaureate" => "الأول بكالوريا", "SecondBaccalaureate" => "الثاني بكالوريا",
        _ => NumberedGradeLabel(grade)
    };

    private static string NumberedGradeLabel(string grade)
    {
        string[] ordinals = ["الأول", "الثاني", "الثالث", "الرابع", "الخامس", "السادس"];
        (string Prefix, string Label)[] groups = [("PrimaryGrade", "الابتدائي"), ("PrepGrade", "الإعدادي"),
            ("AzhariPrimary", "الابتدائي الأزهري"), ("AzhariPrep", "الإعدادي الأزهري"), ("AzhariSecondary", "الثانوي الأزهري")];
        foreach (var group in groups)
            if (grade.StartsWith(group.Prefix, StringComparison.Ordinal)
                && int.TryParse(grade[group.Prefix.Length..], out var number) && number >= 1 && number <= ordinals.Length)
                return $"{ordinals[number - 1]} {group.Label}";
        return grade.StartsWith("AmericanGrade", StringComparison.Ordinal) ? grade.Replace("AmericanGrade", "Grade ", StringComparison.Ordinal) : grade;
    }
}
