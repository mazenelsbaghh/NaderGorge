using NaderGorge.Application.Features.MimStudio;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NaderGorge.Application.Tests.MimGames;

public sealed class MimStudioContractTests
{
    private static readonly Guid Chapter = Guid.Parse("4f2771eb-8c78-4f45-880b-dbdf6a4873a6");

    [Theory]
    [InlineData(4, 10)] // Overlap.
    [InlineData(6, 10)] // Gap.
    [InlineData(5, 5)] // Empty shot.
    public void RejectsDiscontinuousShotTimelines(int start, int end)
    {
        var document = Episode();
        document.Scenes[0].Shots[1] = document.Scenes[0].Shots[1] with { Start = start, End = end };
        Assert.Throws<ArgumentException>(() => MimStudioContract.Validate(document, new HashSet<Guid> { Chapter }));
    }

    [Fact]
    public void RejectsChaptersFromAnotherLesson()
    {
        Assert.Throws<ArgumentException>(() => MimStudioContract.Validate(Episode(), new HashSet<Guid> { Guid.NewGuid() }));
    }

    [Fact]
    public void AcceptsFourCompleteGroundedScenesButRejectsAnIncompleteEnding()
    {
        var saved = JsonSerializer.SerializeToNode(Episode())!.AsObject();
        saved.Remove("TargetSceneCount");
        saved.Remove("EpisodeContext");
        var document = saved.Deserialize<MimStudioDocument>()!;
        MimStudioContract.Validate(document, new HashSet<Guid> { Chapter });
        document.Scenes[3].Shots[^1] = document.Scenes[3].Shots[^1] with { End = 29 };
        Assert.Throws<ArgumentException>(() => MimStudioContract.Validate(document, new HashSet<Guid> { Chapter }));
    }

    [Fact]
    public void SavesOneSceneFromPastedLessonTextWithoutInventedChapterIds()
    {
        var episode = Episode();
        var doc = episode with { Scenes = [episode.Scenes[0] with { SourceChapterIds = [] }], SourceText = "شرح الحصة" };
        MimStudioContract.Validate(doc, new HashSet<Guid>());
        Assert.Throws<ArgumentException>(() => MimStudioContract.Validate(doc with { Scenes = [] }, new HashSet<Guid>()));
    }

    private static MimStudioDocument Episode() => new(1, "مغامرة ميم", "إنقاذ ميم من المصباح", "كرتون سينمائي", "ثبات الشخصيات",
        Enumerable.Range(0, 4).Select(index => new MimScene($"مشهد {index}", "فكرة من الحصة", [Chapter],
            Enumerable.Range(0, 6).Select(shot => new MimShot(shot * 5, (shot + 1) * 5, "كادر", "حركة", "كاميرا", "", "")).ToArray(),
            "A cinematic scene with the approved references.")).ToArray());
}
