using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Enums;
using Xunit;

namespace NaderGorge.Application.Tests;

public class LessonYouTubeLinksTests
{
    [Theory]
    [InlineData("https://youtu.be/AbCdEf123_-?t=34")]
    [InlineData("https://www.youtube.com/watch?feature=share&v=AbCdEf123_-")]
    [InlineData("https://www.youtube-nocookie.com/embed/AbCdEf123_-")]
    [InlineData("youtube.com/shorts/AbCdEf123_-")]
    public async Task StudentNotificationReplacesRegisteredVideoAndPreservesPublicGuide(string source)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01123456789");
        var video = new LessonVideo { LessonId = Guid.NewGuid(), Provider = "youtube", ProviderVideoId = "AbCdEf123_-" };
        const string publicGuide = "https://youtu.be/EdCbAf123_-";
        db.LessonVideos.Add(video);
        db.NotificationEvents.Add(new NotificationEvent
        {
            UserId = student.Id,
            ChannelType = NotificationChannelType.InApp,
            Title = "فيديو الدرس",
            Body = $"شفت {source}؟ دليل عام {publicGuide}",
            ActionUrl = source
        });
        await db.SaveChangesAsync();

        var response = await new GetStudentNotificationsQueryHandler(db).Handle(new(student.Id), CancellationToken.None);

        var notification = Assert.Single(response.Data!);
        var lessonPath = $"/student/lessons/{video.LessonId:D}?videoId={video.Id:D}";
        Assert.Equal(lessonPath, notification.ActionUrl);
        Assert.Equal($"شفت {lessonPath}؟ دليل عام {publicGuide}", notification.Body);
    }

    [Fact]
    public async Task SharedSourceUsesItsKnownLessonAndAvoidsGuessingAcrossPackages()
    {
        await using var db = TestAppDbContextFactory.Create();
        const string source = "https://youtu.be/AbCdEf123_-";
        var first = new LessonVideo { LessonId = Guid.NewGuid(), Provider = "youtube", ProviderVideoId = "AbCdEf123_-" };
        var second = new LessonVideo { LessonId = Guid.NewGuid(), Provider = "youtube", ProviderVideoId = "AbCdEf123_-" };
        db.LessonVideos.AddRange(first, second);
        await db.SaveChangesAsync();

        var links = await LessonYouTubeLinks.ResolveAsync(db, [source], CancellationToken.None);

        Assert.Equal($"/student/lessons/{second.LessonId:D}?videoId={second.Id:D}", links.FindLessonPath(source, second.LessonId));
        Assert.Equal("/student/lessons", links.FindLessonPath(source));
    }
}
