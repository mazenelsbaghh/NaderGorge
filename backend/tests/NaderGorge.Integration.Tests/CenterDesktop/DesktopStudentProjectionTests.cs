using System.Text.Json;
using NaderGorge.API.CenterDesktop;

namespace NaderGorge.Integration.Tests.CenterDesktop;

public sealed class DesktopStudentProjectionTests
{
    [Theory]
    [InlineData("احمد")]
    [InlineData("٠٠١٢٣")]
    [InlineData("+20 100 000 0123")]
    [InlineData("٠١١٠٠٠٠٠١٢٣")]
    public void StudentIdentifiersFindTheSameStudentAndExposeTheActualCenter(string query)
    {
        using var source = JsonDocument.Parse("""
        {"students":[{"id":"s1","name":"أحمد","code":"00123","phone":"01000000123","guardianPhone":"201100000123","groupIds":["g1"]}],
         "groups":[{"id":"g1","name":"الجمعة","centerId":"c1","gradeId":"r1"}],
         "catalogs":[{"id":"c1","name":"سنتر الاختبار"},{"id":"r1","name":"الصف الأول"}]}
        """);
        var found = JsonSerializer.SerializeToElement(DesktopStudentProjection.Read(source.RootElement, query, null));
        Assert.Equal(1, found.GetProperty("total").GetInt32());
        Assert.Equal("سنتر الاختبار", found.GetProperty("students")[0].GetProperty("centers")[0].GetString());
        var wrongCenter = JsonSerializer.SerializeToElement(DesktopStudentProjection.Read(source.RootElement, new DesktopStudentQuery(Search: query, CenterId: "another-center")));
        Assert.Equal(0, wrongCenter.GetProperty("total").GetInt32());
    }

    [Fact]
    public void LessonRowsJoinActualAttendanceTimePackagePaymentAndEveryExamWithoutInventingAbsence()
    {
        using var source = JsonDocument.Parse("""
        {
          "students":[{"id":"s1","name":"طالب الاختبار","groupIds":["g1"]}],
          "groups":[{"id":"g1","name":"الجمعة","centerId":"c1"},{"id":"g2","name":"الخميس"}],
          "catalogs":[{"id":"c1","name":"سنتر الاختبار"}],
          "sessions":[
            {"id":"l1","groupId":"g1","number":1,"startsAt":"2026-11-01T10:00:00Z","startedAt":"2026-11-01T10:00:00Z"},
            {"id":"l2","groupId":"g1","number":2,"monthNumber":1,"startsAtKnown":false,"startedAt":"2026-11-08T10:00:00Z"},
            {"id":"l3","groupId":"g1","number":3,"startedAt":"2026-11-15T10:00:00Z"},
            {"id":"future","groupId":"g1","number":4},
            {"id":"canceled","groupId":"g1","number":5,"status":"canceled","startedAt":"2026-11-15T10:00:00Z"}],
          "attendances":[
            {"id":"a1","studentId":"s1","sessionId":"l1","status":"present","packageId":"pkg1","recordedAt":"2026-11-01T10:13:00Z"},
            {"id":"a2","studentId":"s1","sessionId":"l2","status":"makeup","packageId":"pkg1","makeupSourceGroupId":"g2","recordedAt":"2026-11-08T10:15:00Z","recordedAtKnown":false},
            {"id":"void","studentId":"s1","sessionId":"l3","status":"present"}],
          "corrections":[{"attendanceId":"void"},{"paymentId":"void-payment","voidsPayment":true}],
          "packages":[{"id":"pkg1","studentId":"s1","paymentId":"p1","monthPlanName":"شهر كامل"}],
          "payments":[{"id":"p1","studentId":"s1","sessionId":"l1","netAmount":21000,"paidAmount":21000},
                      {"id":"void-payment","studentId":"s1","sessionId":"l1","netAmount":99999}],
          "academicActivities":[{"id":"e1","sessionId":"l1","kind":"exam","name":"ورقي"},
                                {"id":"e2","sessionId":"l1","kind":"exam","name":"إلكتروني"},
                                {"id":"h1","sessionId":"l1","kind":"homework","name":"واجب"}],
          "academics":[{"id":"r1","activityId":"e1","studentId":"s1","sessionId":"l1","score":0,"maxScore":10},
                       {"id":"r2","activityId":"e2","studentId":"s1","sessionId":"l1","score":9,"maxScore":12},
                       {"id":"r3","activityId":"h1","studentId":"s1","sessionId":"l1","score":2,"maxScore":3,"homework":"complete"}]
        }
        """);
        var profile = JsonSerializer.SerializeToElement(DesktopStudentProjection.Read(source.RootElement, null, "s1")).GetProperty("profile");
        var lessons = profile.GetProperty("lessons");
        Assert.Equal(3, lessons.GetArrayLength());
        var first = lessons[0];
        Assert.Equal(1, first.GetProperty("month").GetInt32());
        Assert.Equal("2026-11-01T10:13:00Z", first.GetProperty("attendance").GetProperty("recordedAt").GetString());
        Assert.Single(first.GetProperty("payment").GetProperty("receipts").EnumerateArray());
        Assert.Equal(21000, first.GetProperty("payment").GetProperty("receipts")[0].GetProperty("amount").GetInt32());
        var exams = first.GetProperty("exams");
        Assert.Equal(2, exams.GetArrayLength());
        Assert.Equal(0, exams[0].GetProperty("score").GetInt32());
        Assert.Equal(12, exams[1].GetProperty("maxScore").GetInt32());
        Assert.Equal("complete", first.GetProperty("homework").GetString());
        Assert.Equal("", lessons[1].GetProperty("attendance").GetProperty("recordedAt").GetString());
        Assert.Equal("الخميس", lessons[1].GetProperty("attendance").GetProperty("makeupSourceGroup").GetString());
        Assert.Equal("package", lessons[1].GetProperty("payment").GetProperty("coverage").GetString());
        Assert.Equal(JsonValueKind.Null, lessons[2].GetProperty("attendance").ValueKind);
    }

    [Fact]
    public void SearchNormalizesArabicDigitsAndProfileOmitsCredentialsAndCancelledAttendance()
    {
        using var source = JsonDocument.Parse("""
        {
          "credentials":{"password":"NEVER_RETURN"},
          "students":[{"id":"s1","name":"أحمد","code":"00123","phone":"01000000123","groupIds":["g1"],"secret":"NEVER_RETURN"}],
          "groups":[{"id":"g1","name":"مجموعة الجمعة"}],
          "sessions":[{"id":"l1","groupId":"g1","number":2,"monthNumber":1,"startsAt":"2026-01-01","startsAtKnown":false}],
          "attendances":[{"id":"a1","studentId":"s1","sessionId":"l1","status":"present"},{"id":"a2","studentId":"s1","sessionId":"l1","status":"absent"}],
          "corrections":[{"attendanceId":"a2"}],
          "academics":[{"id":"e1","studentId":"s1","sessionId":"l1","score":0,"maxScore":10}]
        }
        """);
        var search = JsonSerializer.SerializeToElement(DesktopStudentProjection.Read(source.RootElement, "٠٠١٢٣", null));
        Assert.Equal(1, search.GetProperty("total").GetInt32());
        var profile = JsonSerializer.SerializeToElement(DesktopStudentProjection.Read(source.RootElement, null, "s1"));
        Assert.DoesNotContain("NEVER_RETURN", profile.ToString());
        var details = profile.GetProperty("profile");
        Assert.Equal(1, details.GetProperty("present").GetInt32());
        Assert.Equal(0, details.GetProperty("absent").GetInt32());
        Assert.Equal("", details.GetProperty("attendances")[0].GetProperty("lesson").GetProperty("date").GetString());
        Assert.Equal(0, details.GetProperty("exams")[0].GetProperty("score").GetInt32());
    }

    [Fact]
    public void NoMatchingStudentHasNoProfileOrOtherStudentRecords()
    {
        using var source = JsonDocument.Parse("""{"students":[{"id":"s1","name":"طالب"}],"attendances":[{"id":"a1","studentId":"s1","status":"present"}]}""");
        var result = JsonSerializer.SerializeToElement(DesktopStudentProjection.Read(source.RootElement, null, "missing"));
        Assert.Equal(0, result.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("profile").ValueKind);
    }
}
