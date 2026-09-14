using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands.MindmapOps;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public class MindmapGenerationReferenceTests
{
    [Fact]
    public async Task GenerateWithoutActiveTeacherPhoto_FailsBeforeTakingProcessingLock()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Mindmap Teacher", "01070000002");
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser };
        var subject = new Subject { Id = Guid.NewGuid(), Name = "Physics", NormalizedName = "PHYSICS" };
        var package = new Package
        {
            Id = Guid.NewGuid(), Name = "Physics Package", SubjectId = subject.Id, Subject = subject,
            TeacherId = teacher.Id, Teacher = teacher, TargetGrade = "3rd Secondary",
        };
        var term = new Term { Id = Guid.NewGuid(), Title = "Term", PackageId = package.Id, Package = package };
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Section", TermId = term.Id, Term = term };
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson", ContentSectionId = section.Id, ContentSection = section };
        var video = new LessonVideo
        {
            Id = Guid.NewGuid(), Title = "Video", Provider = "youtube", ProviderVideoId = "video-id",
            LessonId = lesson.Id, Lesson = lesson,
        };
        video.VideoChapters.Add(new VideoChapter
        {
            Id = Guid.NewGuid(), Title = "Chapter", SummaryText = "Summary", Order = 1,
            LessonVideoId = video.Id, LessonVideo = video,
        });
        db.LessonVideos.Add(video);
        await db.SaveChangesAsync();

        var jobs = new FakeJobEnqueuer();
        var handler = new GenerateChapterMindmapsCommandHandler(db, jobs, new NoOpAiJobCancellationStore());
        var response = await handler.Handle(new GenerateChapterMindmapsCommand(video.Id), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("صورة نشطة", response.Message);
        Assert.Empty(jobs.Jobs);
        Assert.False(await db.LessonVideos.Where(candidate => candidate.Id == video.Id)
            .Select(candidate => candidate.IsProcessingMindmaps).SingleAsync());
    }

    private sealed class NoOpAiJobCancellationStore : IAiJobCancellationStore
    {
        public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task RequestMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
    }
}
