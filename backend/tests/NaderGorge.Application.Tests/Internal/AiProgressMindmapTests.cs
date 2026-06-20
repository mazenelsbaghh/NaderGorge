using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.Internal;

public class AiProgressMindmapTests
{
    [Fact]
    public async Task FailedMindmapJob_ClearsOnlyMindmapProcessingState()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Mindmap Teacher", "01070000001");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "History", NormalizedName = "HISTORY" };
        var package = new Package
        {
            Id = Guid.NewGuid(), Name = "History Package", SubjectId = subject.Id, Subject = subject,
            TeacherId = teacher.Id, Teacher = teacher, TargetGrade = "3rd Secondary",
        };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term", PackageId = package.Id, Package = package };
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Section", TermId = term.Id, Term = term };
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson", ContentSectionId = section.Id, ContentSection = section };
        var video = new LessonVideo
        {
            Id = Guid.NewGuid(), Title = "Video", Provider = "youtube", ProviderVideoId = "video-id",
            LessonId = lesson.Id, Lesson = lesson, IsProcessingAI = true, IsProcessingMindmaps = true,
        };
        db.LessonVideos.Add(video);
        await db.SaveChangesAsync();

        var handler = new AiProgressCommandHandler(db);
        var result = await handler.Handle(
            new AiProgressCommand($"{video.Id}_mindmaps", 0, "failed", "image generation failed"),
            CancellationToken.None);

        var updatedVideo = await db.LessonVideos.SingleAsync(candidate => candidate.Id == video.Id);
        Assert.True(result.Success);
        Assert.False(updatedVideo.IsProcessingMindmaps);
        Assert.True(updatedVideo.IsProcessingAI);
    }
}
