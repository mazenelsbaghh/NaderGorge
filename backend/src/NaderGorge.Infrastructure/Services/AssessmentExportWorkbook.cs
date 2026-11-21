using ClosedXML.Excel;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;

namespace NaderGorge.Infrastructure.Services;

public static class AssessmentExportWorkbook
{
    private static readonly string[] Headers = ["اسم الطالب", "كود الطالب", "هاتف الطالب", "هاتف ولي الأمر", "هاتف الأم",
        "الحالة", "الدرجة", "الدرجة النهائية", "النسبة", "التقييم", "بدأ في (القاهرة)", "سلّم في (القاهرة)", "معرّف الطالب", "معرّف المحاولة"];

    public static byte[] Build(AssessmentExportReport report, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        if (report.Selection == AssessmentExportSelection.Absent)
            WriteRows(workbook, "الغياب", report.Rows, ct);
        else
        {
            var best = report.Rows.GroupBy(r => r.Student.Id).Select(group => group.OrderByDescending(r => r.IsFinal)
                .ThenByDescending(r => r.Maximum > 0 ? r.Score / r.Maximum : null)
                .ThenByDescending(r => r.SubmittedAt).ThenBy(r => r.AttemptId).First()).ToArray();
            WriteRows(workbook, "درجات الطلاب", best, ct);
            WriteRows(workbook, "كل المحاولات", report.Rows, ct);
        }
        WriteMetadata(workbook, report);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteMetadata(XLWorkbook workbook, AssessmentExportReport report)
    {
        var sheet = workbook.Worksheets.Add("بيانات الكشف");
        sheet.RightToLeft = true;
        string[] labels = ["الاسم", "النوع", "معرّف النسخة", "الكود", "نوع الكشف", "وقت الإنشاء (القاهرة)", "تعريف النتيجة", "تعريف الغياب"];
        object[] values = [report.Title, report.Kind.ToString() == "Exam" ? "امتحان" : "واجب", report.AssessmentId.ToString(), report.InternalCode,
            report.Selection == AssessmentExportSelection.Grades ? "الدرجات" : "الغياب", CairoTime.ToLocal(report.GeneratedAtUtc),
            "أعلى نسبة من المحاولات المصححة لكل طالب، مع ورقة بكل المحاولات. النتائج المعلقة موضحة وليست درجات نهائية.",
            "أصحاب الصلاحيات الحالية الذين لم يسلّموا هذه النسخة فقط. التسليم المنتظر للتصحيح لا يُحسب غيابًا."];
        for (var index = 0; index < labels.Length; index++)
        {
            sheet.Cell(index + 1, 1).Value = labels[index];
            sheet.Cell(index + 1, 2).Value = XLCellValue.FromObject(values[index]);
        }
        sheet.Column(1).Width = 28;
        sheet.Column(2).Width = 75;
        sheet.Column(2).Style.Alignment.WrapText = true;
        sheet.Cell(6, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
    }

    private static void WriteRows(XLWorkbook workbook, string title, IReadOnlyList<AssessmentExportRow> rows, CancellationToken ct)
    {
        var sheet = workbook.Worksheets.Add(title);
        sheet.RightToLeft = true;
        for (var column = 0; column < Headers.Length; column++) sheet.Cell(1, column + 1).Value = Headers[column];
        for (var index = 0; index < rows.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            WriteRow(sheet, index + 2, rows[index]);
        }
        sheet.Range(1, 1, 1, Headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#0A1D3D");
        sheet.Range(1, 1, 1, Headers.Length).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, Headers.Length).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, rows.Count + 1, Headers.Length).SetAutoFilter();
        sheet.Columns(1, Headers.Length).Width = 22;
        sheet.Column(1).Width = 32;
        sheet.Columns(13, 14).Width = 38;
        sheet.Column(9).Style.NumberFormat.Format = "0.00%";
        sheet.Columns(11, 12).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
    }

    private static void WriteRow(IXLWorksheet sheet, int row, AssessmentExportRow attempt)
    {
        var student = attempt.Student;
        object?[] values = [student.Name, student.Code, student.Phone, student.ParentPhone, student.MotherPhone,
            attempt.Status, attempt.Score, attempt.Maximum, attempt.Maximum > 0 ? attempt.Score / attempt.Maximum : null,
            attempt.Evaluation, attempt.StartedAt is { } start ? CairoTime.ToLocal(start) : null,
            attempt.SubmittedAt is { } submitted ? CairoTime.ToLocal(submitted) : null, student.Id.ToString(), attempt.AttemptId?.ToString()];
        for (var column = 0; column < values.Length; column++)
            sheet.Cell(row, column + 1).Value = XLCellValue.FromObject(values[column]);
    }
}
