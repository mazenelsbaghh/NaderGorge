using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

public sealed class PlatformFinanceExportService(IAppDbContext db) : IPlatformFinanceExportService
{
    private readonly IAppDbContext _db = db;

    public async Task<FinanceExportResult> ExportLedgerAsync(string format, DateTime from, DateTime to, Guid actorUserId, CancellationToken ct)
    {
        if (format is not ("xlsx" or "pdf")) throw new ArgumentException("FINANCE_EXPORT_FORMAT");
        var startDate = from.Date;
        var endDate = to.Date.AddDays(1);
        var rows = await (from entry in _db.JournalEntries.AsNoTracking()
                          join line in _db.JournalLines.AsNoTracking() on entry.Id equals line.JournalEntryId
                          join account in _db.FinancialAccounts.AsNoTracking() on line.FinancialAccountId equals account.Id
                          where entry.Status == JournalEntryStatus.Posted && entry.OccurredAt >= startDate && entry.OccurredAt < endDate
                          orderby entry.OccurredAt descending, entry.SequenceNumber descending
                          select new FinanceExportRow(entry.OccurredAt, entry.SequenceNumber, entry.Description, account.Code, account.Name, line.Debit, line.Credit))
            .ToListAsync(ct);

        QuestPDF.Settings.License = LicenseType.Community;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return format == "xlsx"
            ? new FinanceExportResult(BuildXlsx(rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"platform-finance-{timestamp}.xlsx")
            : new FinanceExportResult(BuildPdf(rows), "application/pdf", $"platform-finance-{timestamp}.pdf");
    }

    private static byte[] BuildXlsx(IReadOnlyList<FinanceExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("المركز المالي");
        sheet.RightToLeft = true;
        var headers = new[] { "التاريخ", "رقم القيد", "الوصف", "الحساب", "اسم الحساب", "مدين", "دائن" };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
        sheet.Row(1).Style.Font.Bold = true;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            sheet.Cell(index + 2, 1).Value = row.OccurredAt.ToString("yyyy-MM-dd");
            sheet.Cell(index + 2, 2).Value = row.SequenceNumber;
            sheet.Cell(index + 2, 3).Value = row.Description;
            sheet.Cell(index + 2, 4).Value = row.Code;
            sheet.Cell(index + 2, 5).Value = row.Name;
            sheet.Cell(index + 2, 6).Value = row.Debit;
            sheet.Cell(index + 2, 7).Value = row.Credit;
        }
        sheet.Columns().AdjustToContents(5, 60);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildPdf(IReadOnlyList<FinanceExportRow> rows) => Document.Create(document => document.Page(page =>
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(20);
        page.DefaultTextStyle(x => x.FontSize(8));
        page.Header().AlignRight().Text("تقرير المركز المالي العام").Bold().FontSize(16);
        page.Content().Table(table =>
        {
            table.ColumnsDefinition(columns => { for (var i = 0; i < 7; i++) columns.RelativeColumn(); });
            foreach (var header in new[] { "التاريخ", "القيد", "الوصف", "الحساب", "اسم الحساب", "مدين", "دائن" })
                table.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(header).Bold();
            foreach (var row in rows)
            {
                foreach (var value in new[] { row.OccurredAt.ToString("yyyy-MM-dd"), row.SequenceNumber.ToString(), row.Description, row.Code, row.Name, row.Debit.ToString("N2"), row.Credit.ToString("N2") })
                    table.Cell().Padding(3).Text(value);
            }
        });
    })).GeneratePdf();

    private sealed record FinanceExportRow(DateTime OccurredAt, long SequenceNumber, string Description, string Code, string Name, decimal Debit, decimal Credit);
}
