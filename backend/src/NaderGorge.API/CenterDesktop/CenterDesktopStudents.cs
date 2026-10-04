using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NaderGorge.API.CenterDesktop;

public sealed partial class CenterDesktopSupportClient
{
    public async Task<object> StudentsAsync(string id, string? query, string? studentId, CancellationToken token)
    {
        ValidateId(id);
        if ((query?.Length ?? 0) > 100 || (studentId?.Length ?? 0) > 100) throw BadRequest();
        using var download = await DownloadAsync(id, token);
        using var bytes = new MemoryStream();
        await download.CopyToAsync(bytes, token);
        try
        {
            bytes.Position = 0;
            using var document = await JsonDocument.ParseAsync(bytes, new JsonDocumentOptions { MaxDepth = 32 }, token);
            var root = document.RootElement;
            if (Text(root, "kind") != "database" || !root.TryGetProperty("data", out var backup) ||
                backup.ValueKind != JsonValueKind.Object || !backup.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new DesktopSupportException(400, "هذه الرفعة لا تحتوي على قاعدة بيانات طلاب.");
            return DesktopStudentProjection.Read(data, query, studentId);
        }
        catch (JsonException) { throw InvalidResponse(); }
    }

    private static string Text(JsonElement row, string key) => DesktopStudentProjection.Text(row, key);
}

/// Only student-facing fields leave the private bundle; staff credentials and
/// arbitrary snapshot properties are never returned to the browser.
public static class DesktopStudentProjection
{
    public static object Read(JsonElement data, string? query, string? studentId)
    {
        var search = Normalize(query ?? "");
        var groups = Rows(data, "groups").GroupBy(g => Text(g, "id")).ToDictionary(g => g.Key, g => Text(g.First(), "name"));
        var students = Rows(data, "students").Where(s => studentId != null ? Text(s, "id") == studentId :
            search.Length == 0 || new[] { "name", "code", "barcode", "phone", "guardianPhone" }.Any(k => Normalize(Text(s, k)).Contains(search))).ToArray();
        var results = students.Take(50).Select(s => new
        {
            id = Text(s, "id"), name = Text(s, "name"), code = Text(s, "code"), barcode = Text(s, "barcode"),
            phone = Text(s, "phone"), guardianPhone = Text(s, "guardianPhone"), notes = Text(s, "notes"),
            discountPercent = Number(s, "discountPercent"), suspended = Flag(s, "isSuspended"),
            groups = Rows(s, "groupIds").Where(g => g.ValueKind == JsonValueKind.String).Select(g => groups.GetValueOrDefault(g.GetString()!, g.GetString()!)).ToArray()
        }).ToArray();
        object? profile = null;
        if (studentId != null && students.Length != 0)
        {
            var sessions = Rows(data, "sessions").GroupBy(s => Text(s, "id")).ToDictionary(s => s.Key, s => s.First());
            var canceled = Rows(data, "corrections").Select(c => Text(c, "attendanceId")).ToHashSet();
            var attendance = Rows(data, "attendances").Where(a => Text(a, "studentId") == studentId && !canceled.Contains(Text(a, "id"))).ToArray();
            var exams = Rows(data, "academics").Where(a => Text(a, "studentId") == studentId).ToArray();
            object Lesson(JsonElement row)
            {
                sessions.TryGetValue(Text(row, "sessionId"), out var session);
                return new { group = groups.GetValueOrDefault(Text(session, "groupId"), "غير متوفر"),
                    number = Number(session, "number"), month = Number(session, "monthNumber"),
                    date = session.ValueKind == JsonValueKind.Object && session.TryGetProperty("startsAtKnown", out var known) && known.ValueKind == JsonValueKind.False ? "" : Text(session, "startsAt") };
            }
            profile = new
            {
                present = attendance.Count(a => Text(a, "status") is "present" or "makeup"),
                absent = attendance.Count(a => Text(a, "status") == "absent"),
                attendanceTotal = attendance.Length, examTotal = exams.Length,
                attendances = attendance.OrderByDescending(a => Text(a, "recordedAt")).Take(500).Select(a => new { id = Text(a, "id"), status = Text(a, "status"), lesson = Lesson(a) }).ToArray(),
                exams = exams.OrderByDescending(a => Text(a, "updatedAt")).Take(500).Select(a => new { id = Text(a, "id"), score = Number(a, "score"), maxScore = a.TryGetProperty("maxScoreKnown", out var known) && known.ValueKind == JsonValueKind.False ? null : Number(a, "maxScore"), absent = Flag(a, "examAbsent"), homework = Text(a, "homework"), lesson = Lesson(a) }).ToArray()
            };
        }
        return new { students = results, total = students.Length, profile };
    }

    internal static string Text(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static decimal? Number(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) ? n : null;
    private static bool Flag(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    private static IEnumerable<JsonElement> Rows(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    private static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (var c in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c == 'ـ') continue;
            result.Append(c switch { >= '٠' and <= '٩' => (char)('0' + c - '٠'), >= '۰' and <= '۹' => (char)('0' + c - '۰'), 'ى' => 'ي', _ => c });
        }
        return result.ToString();
    }
}
