using System.Text.Json;

namespace NaderGorge.API.CenterDesktop;

public static partial class DesktopStudentProjection
{
    private sealed record StudentHistorySource(JsonElement Data, JsonElement Student,
        Dictionary<string, JsonElement> Groups, Dictionary<string, string> Catalogs,
        JsonElement[] Attendance, JsonElement[] Academics);

    private static object[] Lessons(StudentHistorySource source)
    {
        var data = source.Data;
        var student = source.Student;
        var groups = source.Groups;
        var catalogs = source.Catalogs;
        var attendance = source.Attendance;
        var academics = source.Academics;
        var studentId = Text(student, "id");
        var memberships = Rows(student, "groupIds").Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToHashSet();
        var history = attendance.Concat(academics).Select(a => Text(a, "sessionId")).ToHashSet();
        var corrections = Rows(data, "corrections").ToArray();
        var voidedPayments = corrections.Where(c => Flag(c, "voidsPayment")).Select(c => Text(c, "paymentId")).ToHashSet();
        var voidedPackages = corrections.Where(c => Flag(c, "voidsPackage")).Select(c => Text(c, "packageId")).ToHashSet();
        var payments = Rows(data, "payments").Where(p => Text(p, "studentId") == studentId && !voidedPayments.Contains(Text(p, "id"))).ToArray();
        var packages = Rows(data, "packages").Where(p => Text(p, "studentId") == studentId && !voidedPackages.Contains(Text(p, "id"))).ToArray();
        var settlements = Rows(data, "debtSettlements").Where(p => Text(p, "studentId") == studentId && Text(p, "kind") == "lesson").ToArray();
        var refunds = Rows(data, "refunds").Where(p => Text(p, "studentId") == studentId).ToArray();
        var fees = Rows(data, "centerFees").Where(p => Text(p, "studentId") == studentId).ToArray();
        var activities = Rows(data, "academicActivities").GroupBy(a => Text(a, "id")).ToDictionary(g => g.Key, g => g.First());
        var months = Rows(data, "studyMonths").ToArray();
        var result = new List<object>();

        foreach (var session in Rows(data, "sessions").Where(s => Text(s, "status") != "canceled" &&
            (history.Contains(Text(s, "id")) || memberships.Contains(Text(s, "groupId")) &&
                (Text(s, "startedAt").Length != 0 || Rows(s, "importRoster").Any(v => v.ValueKind == JsonValueKind.String && v.GetString() == studentId))))
            .OrderByDescending(s => Number(s, "monthNumber") ?? 1).ThenBy(s => Number(s, "number")))
        {
            var id = Text(session, "id");
            var groupId = Text(session, "groupId");
            groups.TryGetValue(groupId, out var group);
            var entry = attendance.Where(a => Text(a, "sessionId") == id).OrderByDescending(a => Text(a, "recordedAt")).FirstOrDefault();
            var packageId = Text(entry, "packageId");
            var package = packages.FirstOrDefault(p => Text(p, "id") == packageId);
            var rowPayments = payments.Where(p => Text(p, "sessionId") == id || Text(package, "paymentId").Length != 0 && Text(p, "id") == Text(package, "paymentId")).ToArray();
            var sourceGroupId = Text(entry, "makeupSourceGroupId");
            var monthNumber = Number(session, "monthNumber") ?? 1;
            var month = months.FirstOrDefault(m => Rows(m, "lessons").Any(l => Text(l, "id") == Text(session, "preparedLessonId")));
            if (month.ValueKind != JsonValueKind.Object) month = months.FirstOrDefault(m => Number(m, "number") == monthNumber);
            var sessionActivities = activities.Values.Where(a => Text(a, "preparedLessonId").Length != 0
                ? Text(a, "preparedLessonId") == Text(session, "preparedLessonId") : Text(a, "sessionId") == id).ToArray();
            var records = academics.Where(a => Text(a, "sessionId") == id).ToArray();
            result.Add(new
            {
                id, groupId, group = Text(group, "name"), center = catalogs.GetValueOrDefault(Text(group, "centerId"), ""),
                number = Number(session, "number"), month = monthNumber, monthName = Text(month, "name"), name = Text(session, "name"),
                date = KnownText(session, "startsAt", "startsAtKnown"),
                attendance = entry.ValueKind == JsonValueKind.Object ? new
                {
                    status = Text(entry, "status"), recordedAt = KnownText(entry, "recordedAt", "recordedAtKnown"),
                    makeupSourceGroup = groups.TryGetValue(sourceGroupId, out var sourceGroup) ? Text(sourceGroup, "name") : "",
                    paymentPending = Flag(entry, "paymentPending")
                } : null,
                payment = new
                {
                    coverage = package.ValueKind == JsonValueKind.Object ? "package" : rowPayments.Length != 0 ? "recorded" : Flag(entry, "paymentPending") ? "pending" : "unknown",
                    packageName = Text(package, "monthPlanName"),
                    receipts = rowPayments.Select(p => new
                    {
                        id = Text(p, "id"), description = Text(p, "description"),
                        amount = Number(p, "paidAmount") ?? Number(p, "netAmount"), expected = Number(p, "netAmount"),
                        settled = settlements.Where(s => Text(s, "paymentId") == Text(p, "id")).Sum(s => Number(s, "amount") ?? 0),
                        refunded = refunds.Where(r => Text(r, "paymentId") == Text(p, "id")).Sum(r => Number(r, "amount") ?? 0),
                        online = corrections.Where(c => Text(c, "paymentId") == Text(p, "id") && Text(c, "action") == "packageOnlineAllocation").Sum(c => Number(c, "onlineAmount") ?? 0)
                    }).ToArray(),
                    centerFeePaid = fees.Where(f => Text(f, "sessionId") == id).Sum(f => Number(f, "paidAmount") ?? 0)
                },
                exams = SessionExams(records, sessionActivities, activities),
                homework = SessionHomework(records, activities)
            });
        }
        return result.ToArray();
    }

    private static object[] SessionExams(JsonElement[] records, JsonElement[] sessionActivities, Dictionary<string, JsonElement> activities)
    {
        var exams = records.Where(a => !activities.TryGetValue(Text(a, "activityId"), out var activity) || Text(activity, "kind") != "homework")
            .Select(a => ExamResult(a, activities)).ToList();
        foreach (var activity in sessionActivities.Where(a => Text(a, "kind") == "exam" && !records.Any(r => Text(r, "activityId") == Text(a, "id"))))
            exams.Add(new { id = Text(activity, "id"), name = Text(activity, "name"), score = (decimal?)null,
                maxScore = KnownNumber(activity, "maxScore", "maxScoreKnown"), absent = false });
        return exams.ToArray();
    }

    private static string SessionHomework(JsonElement[] records, Dictionary<string, JsonElement> activities) =>
        records.Where(a => Text(a, "activityId").Length == 0 || activities.TryGetValue(Text(a, "activityId"), out var activity) && Text(activity, "kind") == "homework")
            .OrderByDescending(a => Text(a, "updatedAt")).Select(a => Text(a, "homework"))
            .FirstOrDefault(s => s.Length != 0) ?? "notReviewed";

    private static object ExamResult(JsonElement record, Dictionary<string, JsonElement> activities)
    {
        activities.TryGetValue(Text(record, "activityId"), out var activity);
        return new { id = Text(record, "id"), name = Text(activity, "name"), score = Number(record, "score"),
            maxScore = KnownNumber(record, "maxScore", "maxScoreKnown"), absent = Flag(record, "examAbsent") };
    }

    private static string KnownText(JsonElement row, string key, string flag) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(flag, out var known) && known.ValueKind == JsonValueKind.False ? "" : Text(row, key);
    private static decimal? KnownNumber(JsonElement row, string key, string flag) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(flag, out var known) && known.ValueKind == JsonValueKind.False ? null : Number(row, key);
}
