using ClosedXML.Excel;
using NaderGorge.Application.Features.Reporting;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NaderGorge.Infrastructure.Services;

public sealed class ReportExportService : IReportExportService
{
    private static readonly IReadOnlyDictionary<string, string> JourneyStatusLabels = new Dictionary<string, string>
    {
        ["purchaseStatus:purchased"] = "اشترى", ["purchaseStatus:notPurchased"] = "لم يشترِ", ["purchaseStatus:expired"] = "اشتراك منتهي",
        ["purchaseStatus:gift"] = "هدية", ["purchaseStatus:code"] = "بكود", ["purchaseStatus:balance"] = "من الرصيد",
        ["attendanceStatus:present"] = "حاضر", ["attendanceStatus:absent"] = "غائب",
        ["videoStatus:watched"] = "شاهد فيديو", ["videoStatus:notWatched"] = "لم يشاهد",
        ["examStatus:passed"] = "ناجح", ["examStatus:failed"] = "راسب", ["examStatus:notAttempted"] = "لم يمتحن", ["examStatus:noExam"] = "لا يوجد امتحان",
        ["homeworkStatus:submitted"] = "سلّم الواجب", ["homeworkStatus:notSubmitted"] = "لم يسلّم", ["homeworkStatus:noHomework"] = "لا يوجد واجب"
    };
    private readonly IReportQueryService _reports;
    private readonly IAppDbContext _db;

    public ReportExportService(IReportQueryService reports, IAppDbContext db)
    {
        _reports = reports;
        _db = db;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ReportExportDto> ExportAsync(string format, ExecuteReportRequest request, Guid actorUserId, bool isTeacher, CancellationToken ct)
    {
        if (format is not ("xlsx" or "pdf")) throw new ArgumentException("صيغة التصدير يجب أن تكون xlsx أو pdf.");
        var report = await _reports.ExecuteForExportAsync(request, actorUserId, isTeacher, ct);
        var rows = report.Rows;

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var export = format == "xlsx"
            ? new ReportExportDto(BuildXlsx(report, rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"report-{request.Domain}-{timestamp}.xlsx")
            : new ReportExportDto(BuildPdf(report, rows), "application/pdf", $"report-{request.Domain}-{timestamp}.pdf");
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "ReportExported",
            EntityType = "Report",
            PerformedByUserId = actorUserId,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { request.Domain, Format = format, RowCount = rows.Count, IsTeacher = isTeacher })
        });
        await _db.SaveChangesAsync(ct);
        return export;
    }

    private static byte[] BuildXlsx(ReportResultDto result, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        sheet.Cell(1, 1).Value = $"تقرير {DomainLabel(result.Domain)}";
        sheet.Cell(2, 1).Value = $"وقت الإنشاء (القاهرة): {result.GeneratedAtCairo:yyyy-MM-dd HH:mm}";
        sheet.Range(1, 1, 1, Math.Max(1, result.Columns.Count)).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        for (var column = 0; column < result.Columns.Count; column++)
        {
            sheet.Cell(4, column + 1).Value = result.Columns[column].Label;
            sheet.Cell(4, column + 1).Style.Font.SetBold();
        }
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        for (var column = 0; column < result.Columns.Count; column++)
            sheet.Cell(rowIndex + 5, column + 1).Value = XLCellValue.FromObject(TranslateStatus(result.Columns[column].Key, rows[rowIndex].GetValueOrDefault(result.Columns[column].Key)));
        sheet.Columns().AdjustToContents(5, 60);
        sheet.RightToLeft = true;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildPdf(ReportResultDto result, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(20);
            page.DefaultTextStyle(style => style.FontSize(8));
            page.Header().Column(header =>
            {
                header.Item().AlignRight().Text($"تقرير {DomainLabel(result.Domain)}").Bold().FontSize(16);
                header.Item().AlignRight().Text($"وقت الإنشاء (القاهرة): {result.GeneratedAtCairo:yyyy-MM-dd HH:mm}");
            });
            page.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(columns => { foreach (var _ in result.Columns) columns.RelativeColumn(); });
                table.Header(header => { foreach (var column in result.Columns) header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(column.Label).Bold(); });
                foreach (var row in rows)
                foreach (var column in result.Columns)
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(Format(column.Key, row.GetValueOrDefault(column.Key)));
            });
            page.Footer().AlignCenter().Text(text => { text.Span("صفحة "); text.CurrentPageNumber(); });
        })).GeneratePdf();
    }

    private static string DomainLabel(string domain) => domain == ReportDomains.StudentJourney ? "رحلة الطالب" : domain;

    private static object? TranslateStatus(string field, object? value) =>
        value != null && JourneyStatusLabels.TryGetValue($"{field}:{value}", out var translated) ? translated : value;

    private static string Format(string field, object? value)
    {
        if (value == null) return string.Empty;
        var displayValue = TranslateStatus(field, value);
        return displayValue is DateTime date ? date.ToString("yyyy-MM-dd HH:mm") : displayValue?.ToString() ?? string.Empty;
    }
}
