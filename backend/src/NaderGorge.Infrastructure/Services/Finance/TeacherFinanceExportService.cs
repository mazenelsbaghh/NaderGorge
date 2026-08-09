using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

public sealed class TeacherFinanceExportService(IAppDbContext db) : ITeacherFinanceExportService
{
    private readonly IAppDbContext _db = db;

    public async Task<FinanceExportResult> ExportDayAsync(Guid teacherUserId, DateTime date, CancellationToken ct)
    {
        var teacherId = await _db.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == teacherUserId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(ct);

        if (teacherId is null) throw new InvalidOperationException("TEACHER_ACCOUNT_NOT_FOUND");

        var (from, toExclusive) = CairoTime.GetDayRangeUtc(date);
        var allocations = await _db.TeacherFinancialAllocations
            .AsNoTracking()
            .Include(allocation => allocation.TeacherFinancialEvent)
            .Where(allocation => allocation.TeacherId == teacherId.Value
                && allocation.TeacherFinancialEvent.OccurredAt >= from
                && allocation.TeacherFinancialEvent.OccurredAt < toExclusive)
            .OrderByDescending(allocation => allocation.TeacherFinancialEvent.OccurredAt)
            .ToListAsync(ct);
        var rows = allocations.Select(allocation => new ExportRow(
                allocation.TeacherFinancialEvent.OccurredAt,
                allocation.StudentNameSnapshot ?? "طالب غير معروف",
                allocation.StudentPhoneSnapshot,
                allocation.ContentNameSnapshot,
                allocation.CodeSerialNumber,
                allocation.TeacherFinancialEvent.PaidAmount,
                allocation.TeacherShareAmount,
                allocation.PlatformShareAmount,
                allocation.TeacherFinancialEvent.SourceType.ToString(),
                allocation.ReviewStatus.ToString(),
                allocation.PayoutStatus.ToString()))
            .ToList();

        var workbookBytes = BuildWorkbook(date, rows);
        return new FinanceExportResult(
            workbookBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"teacher-payments-{date:yyyy-MM-dd}.xlsx");
    }

    private static byte[] BuildWorkbook(DateTime date, IReadOnlyList<ExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("مدفوعات اليوم");
        sheet.RightToLeft = true;
        sheet.Cell("A1").Value = $"مدفوعات يوم {date:yyyy-MM-dd}";
        sheet.Range("A1:K1").Merge().Style.Font.SetBold().Font.SetFontSize(16);

        sheet.Cell("A3").Value = "إجمالي المدفوع";
        sheet.Cell("B3").Value = rows.Sum(row => row.PaidAmount);
        sheet.Cell("D3").Value = "ربح المدرس";
        sheet.Cell("E3").Value = rows.Sum(row => row.TeacherShareAmount);
        sheet.Cell("G3").Value = "عدد العمليات";
        sheet.Cell("H3").Value = rows.Count;
        sheet.Range("A3:H3").Style.Font.SetBold();

        AddHeaders(sheet);

        for (var index = 0; index < rows.Count; index++)
            AddTransactionRow(sheet, index + 6, rows[index]);

        sheet.Column(1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        sheet.Columns(6, 8).Style.NumberFormat.Format = "#,##0.00 [$جنيه]";
        sheet.SheetView.FreezeRows(5);
        sheet.Columns().AdjustToContents(5, 45);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddHeaders(IXLWorksheet sheet)
    {
        var headers = new[]
        {
            "التاريخ والوقت", "اسم الطالب", "رقم الهاتف", "المحتوى", "رقم الكود",
            "دفع الطالب", "ربح المدرس", "حصة المنصة", "مصدر العملية", "حالة المراجعة", "حالة الصرف"
        };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(5, index + 1).Value = headers[index];
        sheet.Row(5).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0A1D3D"));
        sheet.Row(5).Style.Font.SetFontColor(XLColor.White);
    }

    private static void AddTransactionRow(IXLWorksheet sheet, int excelRow, ExportRow row)
    {
        sheet.Cell(excelRow, 1).Value = CairoTime.ToLocal(row.OccurredAt);
        sheet.Cell(excelRow, 2).Value = row.StudentName;
        sheet.Cell(excelRow, 3).Value = row.StudentPhone ?? string.Empty;
        sheet.Cell(excelRow, 4).Value = row.ContentName;
        if (row.CodeSerialNumber.HasValue) sheet.Cell(excelRow, 5).Value = row.CodeSerialNumber.Value;
        sheet.Cell(excelRow, 6).Value = row.PaidAmount;
        sheet.Cell(excelRow, 7).Value = row.TeacherShareAmount;
        sheet.Cell(excelRow, 8).Value = row.PlatformShareAmount;
        sheet.Cell(excelRow, 9).Value = row.SourceType;
        sheet.Cell(excelRow, 10).Value = row.ReviewStatus;
        sheet.Cell(excelRow, 11).Value = row.PayoutStatus;
    }

    private sealed record ExportRow(
        DateTime OccurredAt,
        string StudentName,
        string? StudentPhone,
        string ContentName,
        long? CodeSerialNumber,
        decimal PaidAmount,
        decimal TeacherShareAmount,
        decimal PlatformShareAmount,
        string SourceType,
        string ReviewStatus,
        string PayoutStatus);
}
