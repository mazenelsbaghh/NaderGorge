using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignService
{
    private const int MaximumSpreadsheetColumns = 100;
    private const int MaximumSpreadsheetRows = 25_000;

    public async Task<WhatsAppCampaignSpreadsheetInspectionDto> InspectSpreadsheetAsync(
        Stream stream,
        string fileName,
        CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var table = extension switch
        {
            ".xlsx" => ReadWorkbook(stream),
            ".csv" => await ReadCsvAsync(stream, ct),
            _ => throw Invalid("صيغة الملف غير مدعومة. استخدم XLSX أو CSV.")
        };
        var headers = ValidateSpreadsheetHeaders(table);
        var rows = SpreadsheetRows(table, headers);
        return new WhatsAppCampaignSpreadsheetInspectionDto(
            Path.GetFileName(fileName), headers, rows);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadWorkbook(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw Invalid("ملف Excel لا يحتوي ورقة عمل.");
        var range = worksheet.RangeUsed();
        if (range is null) throw Invalid("ورقة Excel فارغة.");
        if (range.ColumnCount() > MaximumSpreadsheetColumns || range.RowCount() > MaximumSpreadsheetRows + 1)
            throw Invalid("ملف Excel يتجاوز 100 عمود أو 25,000 صف.");
        return range.Rows().Select(row => (IReadOnlyList<string>)row.Cells()
            .Select(cell => BoundedCell(cell.GetFormattedString())).ToArray()).ToArray();
    }

    private static async Task<IReadOnlyList<IReadOnlyList<string>>> ReadCsvAsync(
        Stream stream,
        CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(ct);
        var rows = ParseCsv(content);
        if (rows.Count > MaximumSpreadsheetRows + 1 || rows.Any(row => row.Count > MaximumSpreadsheetColumns))
            throw Invalid("ملف CSV يتجاوز 100 عمود أو 25,000 صف.");
        return rows;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string content)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"' && quoted && index + 1 < content.Length && content[index + 1] == '"')
            {
                cell.Append('"');
                index++;
            }
            else if (character == '"') quoted = !quoted;
            else if (character == ',' && !quoted) AppendCsvCell(row, cell);
            else if ((character == '\n' || character == '\r') && !quoted)
                AppendCsvRow(rows, row, cell, character, content, ref index);
            else cell.Append(character);
        }
        if (quoted) throw Invalid("ملف CSV يحتوي علامة اقتباس غير مغلقة.");
        if (cell.Length > 0 || row.Count > 0)
        {
            AppendCsvCell(row, cell);
            rows.Add(row.ToArray());
        }
        return rows;
    }

    private static void AppendCsvCell(List<string> row, StringBuilder cell)
    {
        row.Add(BoundedCell(cell.ToString()));
        cell.Clear();
    }

    private static void AppendCsvRow(
        List<IReadOnlyList<string>> rows,
        List<string> row,
        StringBuilder cell,
        char lineEnding,
        string content,
        ref int index)
    {
        AppendCsvCell(row, cell);
        rows.Add(row.ToArray());
        row.Clear();
        if (lineEnding == '\r' && index + 1 < content.Length && content[index + 1] == '\n') index++;
    }

    private static IReadOnlyList<string> ValidateSpreadsheetHeaders(
        IReadOnlyList<IReadOnlyList<string>> table)
    {
        if (table.Count < 2) throw Invalid("الملف يحتاج صف عناوين وصف بيانات واحدًا على الأقل.");
        var headers = table[0].Select(header => header.Normalize().Trim()).ToArray();
        if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace))
            throw Invalid("كل أعمدة الشيت يجب أن تحمل عناوين واضحة.");
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw Invalid("عناوين أعمدة الشيت يجب ألا تتكرر.");
        return headers;
    }

    private static IReadOnlyList<WhatsAppCampaignImportedRowDto> SpreadsheetRows(
        IReadOnlyList<IReadOnlyList<string>> table,
        IReadOnlyList<string> headers)
    {
        return table.Skip(1).Select((cells, index) => new WhatsAppCampaignImportedRowDto(
                index + 2,
                headers.Select((header, columnIndex) => new
                    { header, cell = columnIndex < cells.Count ? cells[columnIndex] : string.Empty })
                    .ToDictionary(entry => entry.header, entry => entry.cell, StringComparer.OrdinalIgnoreCase)))
            .Where(row => row.Columns.Values.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToArray();
    }

    private static string BoundedCell(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormC).Trim();
        if (normalized.Length > MaximumVariableLength)
            throw Invalid("إحدى خلايا الشيت تتجاوز 1,024 حرفًا.");
        return normalized;
    }
}
