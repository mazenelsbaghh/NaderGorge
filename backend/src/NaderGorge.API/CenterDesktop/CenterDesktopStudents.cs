using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace NaderGorge.API.CenterDesktop;

public sealed record DesktopStudentQuery(string? Search = null, string? StudentId = null,
    string? GroupId = null, string? CenterId = null, string? GradeId = null);

public sealed class CenterDesktopStudentCache : IDisposable
{
    internal MemoryCache Snapshots { get; } = new(new MemoryCacheOptions { SizeLimit = 128L * 1024 * 1024 });
    internal SemaphoreSlim Read { get; } = new(1, 1);
    public void Dispose() { Snapshots.Dispose(); Read.Dispose(); }
}

public sealed partial class CenterDesktopSupportClient
{
    public async Task<object> StudentsAsync(string id, DesktopStudentQuery query, CancellationToken token)
    {
        ValidateId(id);
        if (new[] { query.Search, query.StudentId, query.GroupId, query.CenterId, query.GradeId }.Any(v => (v?.Length ?? 0) > 100)) throw BadRequest();
        // Upload IDs are immutable. Partition the private, bounded cache by connection credentials.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_options.BaseUrl + "\n" + _options.AdminToken))) + ":" + id.ToLowerInvariant();
        await _students.Read.WaitAsync(token);
        JsonElement snapshot;
        try
        {
            if (!_students.Snapshots.TryGetValue(key, out snapshot))
            {
                var loaded = await ReadStudentSnapshotAsync(id, token);
                snapshot = loaded.Data;
                _students.Snapshots.Set(key, snapshot, new MemoryCacheEntryOptions { Size = loaded.Size, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
            }
        }
        finally { _students.Read.Release(); }
        token.ThrowIfCancellationRequested();
        return DesktopStudentProjection.Read(snapshot, query);
    }

    private async Task<(JsonElement Data, long Size)> ReadStudentSnapshotAsync(string id, CancellationToken token)
    {
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
            return (data.Clone(), bytes.Length);
        }
        catch (JsonException) { throw InvalidResponse(); }
    }

    private static string Text(JsonElement row, string key) => DesktopStudentProjection.Text(row, key);
}

/// Only student-facing fields leave the private bundle; staff credentials and
/// arbitrary snapshot properties are never returned to the browser.
public static partial class DesktopStudentProjection
{
    public static object Read(JsonElement data, string? query, string? studentId) => Read(data, new DesktopStudentQuery(query, studentId));

    public static object Read(JsonElement data, DesktopStudentQuery query)
    {
        var search = Normalize(query.Search ?? "");
        var studentId = query.StudentId;
        var groupId = query.GroupId;
        var centerId = query.CenterId;
        var gradeId = query.GradeId;
        var groupRows = Rows(data, "groups").GroupBy(g => Text(g, "id")).ToDictionary(g => g.Key, g => g.First());
        var groups = groupRows.ToDictionary(g => g.Key, g => Text(g.Value, "name"));
        var catalogs = Rows(data, "catalogs").GroupBy(g => Text(g, "id")).ToDictionary(g => g.Key, g => Text(g.First(), "name"));
        var matchingGroups = groupRows.Where(g => (string.IsNullOrEmpty(groupId) || g.Key == groupId) &&
            (string.IsNullOrEmpty(centerId) || Text(g.Value, "centerId") == centerId) &&
            (string.IsNullOrEmpty(gradeId) || Text(g.Value, "gradeId") == gradeId)).Select(g => g.Key).ToHashSet();
        var filtered = !string.IsNullOrEmpty(groupId) || !string.IsNullOrEmpty(centerId) || !string.IsNullOrEmpty(gradeId);
        var fields = new[] { "name", "code", "barcode", "phone", "guardianPhone" };
        var students = Rows(data, "students").Where(s => studentId != null ? Text(s, "id") == studentId :
            (!filtered || Rows(s, "groupIds").Any(g => g.ValueKind == JsonValueKind.String && matchingGroups.Contains(g.GetString()!))) &&
            (search.Length == 0 || fields.Any(k => Matches(k, Text(s, k), search))))
            .OrderByDescending(s => search.Length != 0 && fields.Any(k => SearchValue(k, search) is { Length: > 0 } expected && SearchValue(k, Text(s, k)) == expected))
            .ThenBy(s => Text(s, "name"), StringComparer.Ordinal).ToArray();
        var results = students.Take(50).Select(s => new
        {
            id = Text(s, "id"), name = Text(s, "name"), code = Text(s, "code"), barcode = Text(s, "barcode"),
            phone = Text(s, "phone"), guardianPhone = Text(s, "guardianPhone"), notes = Text(s, "notes"),
            discountPercent = Number(s, "discountPercent"), suspended = Flag(s, "isSuspended"),
            groups = Rows(s, "groupIds").Where(g => g.ValueKind == JsonValueKind.String).Select(g => groups.GetValueOrDefault(g.GetString()!, g.GetString()!)).ToArray(),
            centers = Rows(s, "groupIds").Where(g => g.ValueKind == JsonValueKind.String && groupRows.ContainsKey(g.GetString()!))
                .Select(g => catalogs.GetValueOrDefault(Text(groupRows[g.GetString()!], "centerId"), "سنتر غير متوفر")).Distinct().ToArray()
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
                    number = Number(session, "number"), month = session.ValueKind == JsonValueKind.Object ? Number(session, "monthNumber") ?? 1 : (decimal?)null,
                    date = session.ValueKind == JsonValueKind.Object && session.TryGetProperty("startsAtKnown", out var known) && known.ValueKind == JsonValueKind.False ? "" : Text(session, "startsAt") };
            }
            profile = new
            {
                present = attendance.Count(a => Text(a, "status") is "present" or "makeup"),
                absent = attendance.Count(a => Text(a, "status") == "absent"),
                attendanceTotal = attendance.Length, examTotal = exams.Length,
                attendances = attendance.OrderByDescending(a => Text(a, "recordedAt")).Take(500).Select(a => new { id = Text(a, "id"), status = Text(a, "status"), lesson = Lesson(a) }).ToArray(),
                exams = exams.OrderByDescending(a => Text(a, "updatedAt")).Take(500).Select(a => new { id = Text(a, "id"), score = Number(a, "score"), maxScore = a.TryGetProperty("maxScoreKnown", out var known) && known.ValueKind == JsonValueKind.False ? null : Number(a, "maxScore"), absent = Flag(a, "examAbsent"), homework = Text(a, "homework"), lesson = Lesson(a) }).ToArray(),
                lessons = Lessons(new StudentHistorySource(data, students[0], groupRows, catalogs, attendance, exams))
            };
        }
        return new { students = results, total = students.Length, profile,
            groups = groupRows.Select(g => new { id = g.Key, name = Text(g.Value, "name"), centerId = Text(g.Value, "centerId"), center = catalogs.GetValueOrDefault(Text(g.Value, "centerId"), ""), gradeId = Text(g.Value, "gradeId"), grade = catalogs.GetValueOrDefault(Text(g.Value, "gradeId"), "") }).ToArray() };
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
        return string.Join(' ', result.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Matches(string field, string value, string query)
    {
        var expected = SearchValue(field, query);
        return expected.Length != 0 && SearchValue(field, value).Contains(expected, StringComparison.Ordinal);
    }

    private static string SearchValue(string field, string value)
    {
        var normalized = Normalize(value);
        if (field is not ("phone" or "guardianPhone")) return normalized;
        var digits = new string(normalized.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("0020", StringComparison.Ordinal)) digits = digits[4..];
        else if (digits.Length == 12 && digits.StartsWith("20", StringComparison.Ordinal)) digits = digits[2..];
        return digits.Length == 10 && digits.StartsWith('1') ? "0" + digits : digits;
    }
}
