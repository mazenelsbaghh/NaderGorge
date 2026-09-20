using System.Text.Json;
using NaderGorge.Application.Features.MimGames;

namespace NaderGorge.Application.Tests.MimGames;

public class MimGameContractTests
{
    [Fact]
    public void AcceptsExactlyThreeBoundedGroundedMissions()
    {
        var json = ValidJson();
        Assert.True(MimGameContract.TryValidate(json, out var normalized, out var error), error);
        Assert.Equal(3, JsonDocument.Parse(normalized).RootElement.GetProperty("missions").GetArrayLength());
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://attacker.invalid/payload")]
    public void RejectsExecutableOrRemoteText(string unsafeText)
    {
        Assert.False(MimGameContract.TryValidate(ValidJson(unsafeText), out _, out _));
    }

    [Fact]
    public void RejectsOutOfRangeAnswer()
    {
        Assert.False(MimGameContract.TryValidate(ValidJson(correctChoice: 2), out _, out var error));
        Assert.Equal("MIM_INVALID_TASK", error);
    }

    private static string ValidJson(string title = "رحلة الحصة", int correctChoice = 1)
    {
        var source = new[] { new { videoId = Guid.NewGuid(), chapterId = Guid.NewGuid(), startTime = 0, endTime = 60 } };
        var missions = Enumerable.Range(1, 3).Select(index => new
        {
            title = $"مهمة {index}", instruction = "اختر الإجابة الصحيحة", hint = "راجع الفكرة", reward = "ختم المعرفة", icon = "book",
            sourceRefs = source, choices = new[] { "الأول", "الثاني" },
            tasks = Enumerable.Range(1, 3).Select(task => new { label = $"سؤال {task}", icon = "target", correctChoiceIndex = correctChoice, explanation = "تفسير الإجابة" })
        });
        return JsonSerializer.Serialize(new { schemaVersion = 1, title, intro = "مراجعة قصيرة", sourceLabel = "ملخص الحصة", missions });
    }
}
