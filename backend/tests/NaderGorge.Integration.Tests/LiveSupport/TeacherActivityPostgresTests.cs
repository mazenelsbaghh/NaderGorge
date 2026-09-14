using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Teacher;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class TeacherActivityPostgresTests
{
    [Fact]
    public async Task Incident20260907_HighPrecisionWatchTotals_LoadActivityWithoutDecimalOverflow()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var teacher = User("Teacher");
        var package = new Package
        {
            Name = "Activity precision", Teacher = new TeacherProfile { User = teacher },
            Subject = new Subject { Name = "Activity precision", NormalizedName = Guid.NewGuid().ToString("N") }
        };
        var video = new LessonVideo
        {
            Title = "Precision video", Provider = "youtube", ProviderVideoId = "test",
            VideoTypeId = await fixture.Db.VideoTypes.Select(type => type.Id).FirstAsync(),
            Lesson = new Lesson
            {
                Title = "Precision lesson", ContentSection = new ContentSection
                {
                    Title = "Precision section", Term = new Term { Title = "Precision term", Package = package }
                }
            }
        };
        for (var index = 0; index < 3; index++)
        {
            fixture.Db.VideoWatchEvents.Add(new VideoWatchEvent
            {
                User = User($"Student {index}"), LessonVideo = video, WatchCount = 1,
                TimeWatchedInSeconds = 100, ActualWatchedSeconds = 33.333333333333333333333333333m
            });
        }
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var response = await new GetTeacherActivityQueryHandler(fixture.Db)
            .Handle(new GetTeacherActivityQuery(teacher.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(3, response.Data!.ActiveStudents.Count);
        var watched = Assert.Single(response.Data.MostWatchedVideos);
        Assert.Equal(video.Id, watched.VideoId);
        Assert.Equal(3, watched.TotalWatchCount);
        Assert.Equal(300, watched.TotalTimeWatchedSeconds);
        Assert.Equal(3m, watched.AveragePlaybackRate);
        var emptyTeacher = User("Other teacher");
        fixture.Db.TeacherProfiles.Add(new TeacherProfile { User = emptyTeacher });
        await fixture.Db.SaveChangesAsync();
        var isolated = await new GetTeacherActivityQueryHandler(fixture.Db)
            .Handle(new GetTeacherActivityQuery(emptyTeacher.Id), CancellationToken.None);
        Assert.Empty(isolated.Data!.MostWatchedVideos);
        Assert.Empty(isolated.Data.ActiveStudents);
    }

    private static User User(string name) => new()
    {
        FullName = name, PasswordHash = "not-used",
        PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}"
    };
}
