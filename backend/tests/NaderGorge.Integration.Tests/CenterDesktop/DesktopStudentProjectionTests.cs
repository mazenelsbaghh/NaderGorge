using System.Text.Json;
using NaderGorge.API.CenterDesktop;

namespace NaderGorge.Integration.Tests.CenterDesktop;

public sealed class DesktopStudentProjectionTests
{
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
